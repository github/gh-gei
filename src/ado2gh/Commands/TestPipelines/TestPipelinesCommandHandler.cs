using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OctoshiftCLI.Models;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.TestPipelines
{
    public class TestPipelinesCommandHandler : ICommandHandler<TestPipelinesCommandArgs>
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;

        public TestPipelinesCommandHandler(OctoLogger log, AdoApi adoApi)
        {
            _log = log;
            _adoApi = adoApi;
        }

        public async Task Handle(TestPipelinesCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.LogInformation("Starting batch pipeline testing...");

            var testSummary = new PipelineTestSummary();

            var startTime = DateTime.UtcNow;

            try
            {
                // Step 1: Discover pipelines to test
                _log.LogInformation("Step 1: Discovering pipelines...");
                var pipelinesToTest = await DiscoverPipelines(args);
                testSummary.TotalPipelines = pipelinesToTest.Count;

                _log.LogInformation($"Found {pipelinesToTest.Count} pipelines to test");

                if (pipelinesToTest.Count == 0)
                {
                    _log.LogWarning("No pipelines found matching the criteria");
                    return;
                }

                // Step 2: Test pipelines with concurrency control
                _log.LogInformation($"Step 2: Testing pipelines (max concurrent: {args.MaxConcurrentTests})...");

                using var semaphore = new SemaphoreSlim(args.MaxConcurrentTests, args.MaxConcurrentTests);
                var testTasks = pipelinesToTest.Select(async pipeline =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await TestSinglePipeline(args, pipeline);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                var results = await Task.WhenAll(testTasks);
                testSummary.AddResults(results);

                // Step 3: Generate summary statistics
                testSummary.TotalTestTime = DateTime.UtcNow - startTime;
                testSummary.SuccessfulBuilds = results.Count(r => r.IsSuccessful);
                testSummary.FailedBuilds = results.Count(r => r.IsFailed);
                testSummary.TimedOutBuilds = results.Count(r => !r.IsCompleted && r.Status == "timedOut");
                testSummary.ErrorsRewiring = results.Count(r => !r.RewiredSuccessfully);
                testSummary.ErrorsRestoring = results.Count(r => !r.RestoredSuccessfully);

                // Step 4: Generate reports
                GenerateConsoleSummary(testSummary);
                await SaveDetailedReport(testSummary, args.ReportPath);

                _log.LogInformation($"Batch testing completed. Results saved to: {args.ReportPath}");
            }
            catch (Exception ex)
            {
                _log.LogError($"Batch testing failed: {ex.Message}");

                if (testSummary.Results.Any())
                {
                    _log.LogInformation("Generating partial report from completed tests...");
                    testSummary.TotalTestTime = DateTime.UtcNow - startTime;
                    await SaveDetailedReport(testSummary, args.ReportPath);
                }

                throw;
            }
        }

        private async Task<List<(string name, int id)>> DiscoverPipelines(TestPipelinesCommandArgs args)
        {
            // Get all repositories for the team project
            var repos = await _adoApi.GetEnabledRepos(args.AdoOrg, args.AdoTeamProject);
            var pipelines = new List<(string name, int id)>();

            foreach (var repo in repos)
            {
                try
                {
                    var repoPipelines = await _adoApi.GetPipelines(args.AdoOrg, args.AdoTeamProject, repo.Id);

                    foreach (var pipelineName in repoPipelines)
                    {
                        // Apply filter if specified
                        if (!string.IsNullOrEmpty(args.PipelineFilter))
                        {
                            if (!IsMatch(pipelineName, args.PipelineFilter))
                            {
                                continue;
                            }
                        }

                        try
                        {
                            var pipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, pipelineName);
                            pipelines.Add((pipelineName, pipelineId));
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning($"Could not get ID for pipeline '{pipelineName}': {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"Could not get pipelines for repository '{repo.Name}': {ex.Message}");
                }
            }

            return pipelines;
        }

        private bool IsMatch(string text, string pattern)
        {
            // Simple wildcard matching
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
                return true;

            // Convert wildcard pattern to regex
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private async Task<PipelineTestResult> TestSinglePipeline(TestPipelinesCommandArgs args, (string name, int id) pipeline)
        {
            var testResult = new PipelineTestResult
            {
                AdoOrg = args.AdoOrg,
                AdoTeamProject = args.AdoTeamProject,
                PipelineName = pipeline.name,
                PipelineId = pipeline.id,
                StartTime = DateTime.UtcNow,
                PipelineUrl = $"https://dev.azure.com/{args.AdoOrg}/{args.AdoTeamProject}/_build/definition?definitionId={pipeline.id}"
            };

            // Store original pipeline configuration
            string originalRepoName = null;
            string originalRepoId = null;
            string originalDefaultBranch = null;
            string originalClean = null;
            string originalCheckoutSubmodules = null;
            Newtonsoft.Json.Linq.JToken originalTriggers = null;

            try
            {
                _log.LogInformation($"Testing pipeline: {pipeline.name} (ID: {pipeline.id})");

                // Get original repository information
                (originalRepoName, originalRepoId, originalDefaultBranch, originalClean, originalCheckoutSubmodules) =
                    await _adoApi.GetPipelineRepository(args.AdoOrg, args.AdoTeamProject, pipeline.id);
                testResult.AdoRepoName = originalRepoName;

                var (defaultBranch, clean, checkoutSubmodules, triggers) =
                    await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, pipeline.id);
                originalTriggers = triggers;

                // Rewire to GitHub
                await _adoApi.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, pipeline.id,
                    defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo,
                    args.ServiceConnectionId, originalTriggers, args.TargetApiUrl);
                testResult.RewiredSuccessfully = true;

                // Queue build
                var buildId = await _adoApi.QueueBuild(args.AdoOrg, args.AdoTeamProject, pipeline.id, $"refs/heads/{defaultBranch}");
                testResult.BuildId = buildId;

                var (_, _, buildUrl) = await _adoApi.GetBuildStatus(args.AdoOrg, args.AdoTeamProject, buildId);
                testResult.BuildUrl = buildUrl;

                // Restore to ADO immediately after queuing build
                try
                {
                    await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, pipeline.id,
                        originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                    testResult.RestoredSuccessfully = true;
                }
                catch (Exception ex)
                {
                    testResult.ErrorMessage = $"Failed to restore: {ex.Message}";
                    testResult.RestoredSuccessfully = false;
                    _log.LogError($"Failed to restore pipeline {pipeline.name}: {ex.Message}");
                }

                // Monitor build progress
                await MonitorBuildProgress(testResult, args.AdoOrg, args.AdoTeamProject, buildId, args.MonitorTimeoutMinutes);
            }
            catch (Exception ex)
            {
                testResult.ErrorMessage = ex.Message;
                _log.LogError($"Error testing pipeline {pipeline.name}: {ex.Message}");

                // Attempt restoration only if pipeline was rewired but not yet restored
                if (originalRepoName != null && testResult.RewiredSuccessfully && !testResult.RestoredSuccessfully)
                {
                    try
                    {
                        await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, pipeline.id,
                            originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                        testResult.RestoredSuccessfully = true;
                    }
                    catch
                    {
                        testResult.RestoredSuccessfully = false;
                        _log.LogError($"MANUAL RESTORATION REQUIRED for pipeline {pipeline.name} (ID: {pipeline.id})");
                    }
                }
            }

            testResult.EndTime = DateTime.UtcNow;
            return testResult;
        }

        private async Task MonitorBuildProgress(PipelineTestResult testResult, string org, string teamProject, int buildId, int timeoutMinutes)
        {
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - startTime < timeout)
            {
                var (status, result, _) = await _adoApi.GetBuildStatus(org, teamProject, buildId);
                testResult.Status = status;
                testResult.Result = result;

                if (!string.IsNullOrEmpty(result))
                {
                    return; // Build completed
                }

                await Task.Delay(pollInterval);
            }

            testResult.Status = "timedOut";
        }

        private void GenerateConsoleSummary(PipelineTestSummary summary)
        {
            _log.LogInformation("");
            _log.LogInformation("=== PIPELINE BATCH TEST SUMMARY ===");
            _log.LogInformation($"Total Pipelines Tested: {summary.TotalPipelines}");
            _log.LogInformation($"Successful Builds: {summary.SuccessfulBuilds}");
            _log.LogInformation($"Failed Builds: {summary.FailedBuilds}");
            _log.LogInformation($"Timed Out Builds: {summary.TimedOutBuilds}");
            _log.LogInformation($"Rewiring Errors: {summary.ErrorsRewiring}");
            _log.LogInformation($"Restoration Errors: {summary.ErrorsRestoring}");
            _log.LogInformation($"Success Rate: {summary.SuccessRate:F1}%");
            _log.LogInformation($"Total Test Time: {summary.TotalTestTime:hh\\:mm\\:ss}");

            if (summary.ErrorsRestoring > 0)
            {
                _log.LogWarning("");
                _log.LogWarning("PIPELINES REQUIRING MANUAL RESTORATION:");
                foreach (var result in summary.Results.Where(r => !r.RestoredSuccessfully))
                {
                    _log.LogWarning($"  - {result.PipelineName} (ID: {result.PipelineId}) in {result.AdoOrg}/{result.AdoTeamProject}");
                }
            }

            _log.LogInformation("=== END OF SUMMARY ===");
            _log.LogInformation("");
        }

        private async Task SaveDetailedReport(PipelineTestSummary summary, string reportPath)
        {
            var json = JsonConvert.SerializeObject(summary, Formatting.Indented);
            await File.WriteAllTextAsync(reportPath, json);
            _log.LogInformation($"Detailed report saved to: {reportPath}");
        }
    }
}
