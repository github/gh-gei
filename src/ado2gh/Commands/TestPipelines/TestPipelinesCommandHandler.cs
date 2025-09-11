using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.TestPipelines
{
    public class TestPipelinesCommandHandler : ICommandHandler<TestPipelinesCommandArgs>
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;
        private readonly PipelineTestService _pipelineTestService;

        public TestPipelinesCommandHandler(OctoLogger log, AdoApi adoApi)
        {
            _log = log;
            _adoApi = adoApi;
            _pipelineTestService = new PipelineTestService(log, adoApi);
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
                        if (!string.IsNullOrEmpty(args.PipelineFilter) && !IsMatch(pipelineName, args.PipelineFilter))
                        {
                            continue;
                        }

                        try
                        {
                            var pipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, pipelineName);
                            pipelines.Add((pipelineName, pipelineId));
                        }
                        catch (Exception ex) when (ex is not OctoshiftCliException)
                        {
                            _log.LogWarning($"Could not get ID for pipeline '{pipelineName}': {ex.Message}");
                        }
                    }
                }
                catch (Exception ex) when (ex is not OctoshiftCliException)
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
            {
                return true;
            }

            // Convert wildcard pattern to regex
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private async Task<PipelineTestResult> TestSinglePipeline(TestPipelinesCommandArgs args, (string name, int id) pipeline)
        {
            _log.LogInformation($"Testing pipeline: {pipeline.name} (ID: {pipeline.id})");

            var testArgs = new PipelineTestArgs
            {
                AdoOrg = args.AdoOrg,
                AdoTeamProject = args.AdoTeamProject,
                PipelineName = pipeline.name,
                PipelineId = pipeline.id,
                GithubOrg = args.GithubOrg,
                GithubRepo = args.GithubRepo,
                ServiceConnectionId = args.ServiceConnectionId,
                TargetApiUrl = args.TargetApiUrl,
                MonitorTimeoutMinutes = args.MonitorTimeoutMinutes
            };

            return await _pipelineTestService.TestPipeline(testArgs);
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
