using System;
using System.Threading.Tasks;
using OctoshiftCLI.Models;

namespace OctoshiftCLI.Services
{
    /// <summary>
    /// Service for testing Azure DevOps pipelines by temporarily rewiring them to GitHub
    /// </summary>
    public class PipelineTestService
    {
        private readonly OctoLogger _log;
        private readonly AdoApi _adoApi;
        private readonly AdoPipelineTriggerService _pipelineTriggerService;

        public PipelineTestService(OctoLogger log, AdoApi adoApi, AdoPipelineTriggerService pipelineTriggerService = null)
        {
            _log = log;
            _adoApi = adoApi;
            _pipelineTriggerService = pipelineTriggerService ?? (adoApi != null ? new AdoPipelineTriggerService(adoApi, log, "https://dev.azure.com") : null);
        }

        /// <summary>
        /// Tests a single pipeline by temporarily rewiring it to GitHub, running a build, and restoring it
        /// </summary>
        /// <param name="args">Pipeline test arguments</param>
        /// <returns>Test result containing all pipeline test information</returns>
        public async Task<PipelineTestResult> TestPipeline(PipelineTestArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var testResult = new PipelineTestResult
            {
                AdoOrg = args.AdoOrg,
                AdoTeamProject = args.AdoTeamProject,
                PipelineName = args.PipelineName,
                PipelineId = args.PipelineId ?? 0,
                StartTime = DateTime.UtcNow,
                PipelineUrl = $"https://dev.azure.com/{args.AdoOrg}/{args.AdoTeamProject}/_build/definition?definitionId={args.PipelineId}"
            };

            // Store original pipeline configuration for restoration
            string originalRepoName = null;
            string originalDefaultBranch = null;
            string originalClean = null;
            string originalCheckoutSubmodules = null;
            Newtonsoft.Json.Linq.JToken originalTriggers = null;

            try
            {
                // Step 1: Get pipeline information and store original configuration
                if (!args.PipelineId.HasValue)
                {
                    var pipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.PipelineName);
                    testResult.PipelineId = pipelineId;
                    args.PipelineId = pipelineId;
                }

                // Check if pipeline is disabled before attempting to queue a build
                var isEnabled = await _adoApi.IsPipelineEnabled(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value);
                if (!isEnabled)
                {
                    _log.LogWarning($"Pipeline '{args.PipelineName}' (ID: {args.PipelineId.Value}) is disabled. Skipping pipeline test.");
                    testResult.ErrorMessage = "Pipeline is disabled";
                    testResult.EndTime = DateTime.UtcNow;
                    return testResult;
                }

                // Get original repository information for restoration
                (originalRepoName, _, originalDefaultBranch, originalClean, originalCheckoutSubmodules) =
                    await _adoApi.GetPipelineRepository(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value);
                testResult.AdoRepoName = originalRepoName;

                var (defaultBranch, clean, checkoutSubmodules, triggers) =
                    await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value);
                originalTriggers = triggers;

                // Step 2: Rewire to GitHub
                await _pipelineTriggerService.RewirePipelineToGitHub(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value,
                    defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo,
                    args.ServiceConnectionId, originalTriggers, args.TargetApiUrl);
                testResult.RewiredSuccessfully = true;

                // Step 3: Queue a build
                var buildId = await _adoApi.QueueBuild(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value, $"refs/heads/{defaultBranch}");
                testResult.BuildId = buildId;

                var (_, _, buildUrl) = await _adoApi.GetBuildStatus(args.AdoOrg, args.AdoTeamProject, buildId);
                testResult.BuildUrl = buildUrl;

                // Step 4: Restore to ADO immediately after queuing build
                try
                {
                    await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, args.PipelineId.Value,
                        originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                    testResult.RestoredSuccessfully = true;
                }
                catch (Exception ex) when (ex is not OctoshiftCliException)
                {
                    testResult.ErrorMessage = $"Failed to restore: {ex.Message}";
                    testResult.RestoredSuccessfully = false;
                    _log.LogError($"Failed to restore pipeline {args.PipelineName}: {ex.Message}");
                }

                // Step 5: Monitor build progress
                var (finalStatus, finalResult) = await MonitorBuildProgress(args.AdoOrg, args.AdoTeamProject, buildId, args.MonitorTimeoutMinutes, args.PipelineName);
                testResult.Status = finalStatus;
                testResult.Result = finalResult;
            }
            catch (Exception ex) when (ex is not OctoshiftCliException)
            {
                testResult.ErrorMessage = ex.Message;
                testResult.EndTime = DateTime.UtcNow;

                // Attempt restoration only if pipeline was rewired but not yet restored
                if (originalRepoName != null && testResult.RewiredSuccessfully && !testResult.RestoredSuccessfully)
                {
                    try
                    {
                        await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, args.PipelineId ?? 0,
                            originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                        testResult.RestoredSuccessfully = true;
                    }
                    catch (Exception restoreEx) when (restoreEx is not OctoshiftCliException)
                    {
                        testResult.RestoredSuccessfully = false;
                        _log.LogError($"MANUAL RESTORATION REQUIRED for pipeline {args.PipelineName} (ID: {args.PipelineId})");
                    }
                }

                throw new OctoshiftCliException($"Failed to test pipeline '{args.PipelineName}': {ex.Message}", ex);
            }

            testResult.EndTime = DateTime.UtcNow;
            return testResult;
        }

        private async Task<(string status, string result)> MonitorBuildProgress(string org, string teamProject, int buildId, int timeoutMinutes, string pipelineName)
        {
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - startTime < timeout)
            {
                var (status, result, _) = await _adoApi.GetBuildStatus(org, teamProject, buildId);

                if (!string.IsNullOrEmpty(result))
                {
                    return (status, result); // Build completed
                }

                _log.LogInformation($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: Still waiting on pipeline '{pipelineName}' (Build ID: {buildId})");
                await Task.Delay(pollInterval);
            }

            return ("timedOut", null);
        }
    }

    /// <summary>
    /// Arguments for testing a single pipeline
    /// </summary>
    public class PipelineTestArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string PipelineName { get; set; }
        public int? PipelineId { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        public string TargetApiUrl { get; set; }
        public int MonitorTimeoutMinutes { get; set; } = 30;
    }
}
