using System;
using System.Threading;
using System.Threading.Tasks;
using OctoshiftCLI;
using OctoshiftCLI.Models;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;

public class RewirePipelineCommandHandler : ICommandHandler<RewirePipelineCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;

    public RewirePipelineCommandHandler(OctoLogger log, AdoApi adoApi)
    {
        _log = log;
        _adoApi = adoApi;
    }

    public async Task Handle(RewirePipelineCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        // Validate that either pipeline name or ID is provided
        if (string.IsNullOrEmpty(args.AdoPipeline) && !args.AdoPipelineId.HasValue)
        {
            throw new OctoshiftCliException("Either --ado-pipeline or --ado-pipeline-id must be specified");
        }

        if (!string.IsNullOrEmpty(args.AdoPipeline) && args.AdoPipelineId.HasValue)
        {
            throw new OctoshiftCliException("Cannot specify both --ado-pipeline and --ado-pipeline-id. Please use only one.");
        }

        if (args.DryRun)
        {
            await HandleDryRun(args);
        }
        else
        {
            await HandleRegularRewire(args);
        }
    }

    private async Task HandleRegularRewire(RewirePipelineCommandArgs args)
    {
        _log.LogInformation($"Rewiring Pipeline to GitHub repo...");

        var adoPipelineId = await GetPipelineId(args);
        var (defaultBranch, clean, checkoutSubmodules, triggers) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
        await _adoApi.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo, args.ServiceConnectionId, triggers, args.TargetApiUrl);

        _log.LogSuccess("Successfully rewired pipeline");
    }

    private async Task<int> GetPipelineId(RewirePipelineCommandArgs args)
    {
        if (args.AdoPipelineId.HasValue)
        {
            _log.LogInformation($"Using provided pipeline ID: {args.AdoPipelineId.Value}");
            return args.AdoPipelineId.Value;
        }
        
        _log.LogInformation($"Looking up pipeline ID for: {args.AdoPipeline}");
        var pipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
        _log.LogInformation($"Using resolved pipeline ID: {pipelineId}");
        return pipelineId;
    }

    private async Task HandleDryRun(RewirePipelineCommandArgs args)
    {
        _log.LogInformation("Starting dry-run mode: Testing pipeline rewiring to GitHub...");

        var testResult = new PipelineTestResult
        {
            AdoOrg = args.AdoOrg,
            AdoTeamProject = args.AdoTeamProject,
            PipelineName = args.AdoPipeline ?? $"Pipeline ID {args.AdoPipelineId}",
            StartTime = DateTime.UtcNow
        };

        // Store original pipeline configuration for restoration
        string originalRepoName = null;
        string originalRepoId = null;
        string originalDefaultBranch = null;
        string originalClean = null;
        string originalCheckoutSubmodules = null;
        Newtonsoft.Json.Linq.JToken originalTriggers = null;

        try
        {
            // Step 1: Get pipeline information and store original configuration
            _log.LogInformation("Step 1: Retrieving pipeline information...");
            var adoPipelineId = await GetPipelineId(args);
            testResult.PipelineId = adoPipelineId;

            // Get original repository information for restoration
            (originalRepoName, originalRepoId, originalDefaultBranch, originalClean, originalCheckoutSubmodules) = await _adoApi.GetPipelineRepository(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
            testResult.AdoRepoName = originalRepoName;

            var (defaultBranch, clean, checkoutSubmodules, triggers) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
            originalTriggers = triggers;

            // Generate pipeline URLs
            var pipelineUrl = $"https://dev.azure.com/{args.AdoOrg}/{args.AdoTeamProject}/_build/definition?definitionId={adoPipelineId}";
            testResult.PipelineUrl = pipelineUrl;

            _log.LogInformation($"Pipeline ID: {adoPipelineId}");
            _log.LogInformation($"Original ADO Repository: {originalRepoName}");
            _log.LogInformation($"Default branch: {defaultBranch}");

            // Step 2: Rewire to GitHub
            _log.LogInformation("Step 2: Temporarily rewiring pipeline to GitHub...");
            await _adoApi.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo, args.ServiceConnectionId, originalTriggers, args.TargetApiUrl);
            testResult.RewiredSuccessfully = true;
            _log.LogSuccess("Pipeline successfully rewired to GitHub");

            // Step 3: Queue a build
            _log.LogInformation("Step 3: Queuing a test build...");
            var buildId = await _adoApi.QueueBuild(args.AdoOrg, args.AdoTeamProject, adoPipelineId, $"refs/heads/{defaultBranch}");
            testResult.BuildId = buildId;

            var (_, _, buildUrl) = await _adoApi.GetBuildStatus(args.AdoOrg, args.AdoTeamProject, buildId);
            testResult.BuildUrl = buildUrl;

            _log.LogInformation($"Build queued with ID: {buildId}");
            _log.LogInformation($"Build URL: {buildUrl}");

            // Step 4: Rewire back to ADO immediately after queuing build
            _log.LogInformation("Step 4: Restoring pipeline back to original ADO repository...");
            try
            {
                await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                testResult.RestoredSuccessfully = true;
                _log.LogSuccess("Pipeline successfully restored to original ADO repository");
            }
            catch (Exception ex)
            {
                _log.LogError($"Failed to restore pipeline to ADO: {ex.Message}");
                testResult.ErrorMessage = $"Failed to restore: {ex.Message}";
                testResult.RestoredSuccessfully = false;

                // Log detailed information for manual restoration
                _log.LogError("MANUAL RESTORATION REQUIRED:");
                _log.LogError($"  Pipeline ID: {adoPipelineId}");
                _log.LogError($"  Original Repository: {originalRepoName}");
                _log.LogError($"  Pipeline URL: {pipelineUrl}");
            }

            // Step 5: Monitor build progress
            _log.LogInformation($"Step 5: Monitoring build progress (timeout: {args.MonitorTimeoutMinutes} minutes)...");
            await MonitorBuildProgress(testResult, args.AdoOrg, args.AdoTeamProject, buildId, args.MonitorTimeoutMinutes);

            // Step 6: Generate report
            testResult.EndTime = DateTime.UtcNow;
            GenerateReport(testResult);
        }
        catch (Exception ex)
        {
            testResult.EndTime = DateTime.UtcNow;
            testResult.ErrorMessage = ex.Message;
            _log.LogError($"Dry-run failed: {ex.Message}");

            // Attempt restoration only if pipeline was rewired but not yet restored
            if (originalRepoName != null && testResult.RewiredSuccessfully && !testResult.RestoredSuccessfully)
            {
                _log.LogWarning("Attempting to restore pipeline to ADO after error...");
                try
                {
                    await _adoApi.RestorePipelineToAdoRepo(args.AdoOrg, args.AdoTeamProject, testResult.PipelineId, originalRepoName, originalDefaultBranch, originalClean, originalCheckoutSubmodules, originalTriggers);
                    testResult.RestoredSuccessfully = true;
                    _log.LogSuccess("Pipeline restored to ADO after error");
                }
                catch (Exception restoreEx)
                {
                    _log.LogError($"Failed to restore pipeline after error: {restoreEx.Message}");
                    _log.LogError($"MANUAL RESTORATION REQUIRED for Pipeline ID: {testResult.PipelineId}");
                }
            }

            GenerateReport(testResult);
            throw;
        }
    }

    private async Task MonitorBuildProgress(PipelineTestResult testResult, string org, string teamProject, int buildId, int timeoutMinutes)
    {
        var timeout = TimeSpan.FromMinutes(timeoutMinutes);
        var startTime = DateTime.UtcNow;
        var pollInterval = TimeSpan.FromSeconds(30);

        _log.LogInformation("Monitoring build progress...");

        while (DateTime.UtcNow - startTime < timeout)
        {
            var (status, result, _) = await _adoApi.GetBuildStatus(org, teamProject, buildId);
            testResult.Status = status;
            testResult.Result = result;

            _log.LogInformation($"Build status: {status}, Result: {result ?? "N/A"}");

            if (!string.IsNullOrEmpty(result))
            {
                // Build completed
                _log.LogInformation($"Build completed with result: {result}");
                return;
            }

            await Task.Delay(pollInterval);
        }

        // Timeout reached
        _log.LogWarning($"Build monitoring timed out after {timeoutMinutes} minutes");
        testResult.Status = "timedOut";
    }

    private void GenerateReport(PipelineTestResult result)
    {
        _log.LogInformation("");
        _log.LogInformation("=== PIPELINE TEST REPORT ===");
        _log.LogInformation($"ADO Organization: {result.AdoOrg}");
        _log.LogInformation($"ADO Team Project: {result.AdoTeamProject}");
        _log.LogInformation($"Pipeline Name: {result.PipelineName}");
        _log.LogInformation($"Pipeline ID: {result.PipelineId}");
        _log.LogInformation($"Pipeline URL: {result.PipelineUrl}");

        if (result.BuildId.HasValue)
        {
            _log.LogInformation($"Build ID: {result.BuildId}");
            _log.LogInformation($"Build URL: {result.BuildUrl}");
            _log.LogInformation($"Build Status: {result.Status}");
            _log.LogInformation($"Build Result: {result.Result ?? "N/A"}");
        }

        _log.LogInformation($"Test Duration: {result.BuildDuration?.ToString(@"hh\:mm\:ss") ?? "N/A"}");
        _log.LogInformation($"Rewired Successfully: {result.RewiredSuccessfully}");
        _log.LogInformation($"Restored Successfully: {result.RestoredSuccessfully}");

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _log.LogError($"Error: {result.ErrorMessage}");
        }

        if (result.IsSuccessful)
        {
            _log.LogSuccess("✅ Pipeline test PASSED - Build completed successfully");
        }
        else if (result.IsFailed)
        {
            _log.LogError("❌ Pipeline test FAILED - Build failed or was cancelled");
        }
        else if (!result.IsCompleted)
        {
            _log.LogWarning("⏱️ Pipeline test TIMEOUT - Build did not complete within timeout period");
        }

        _log.LogInformation("=== END OF REPORT ===");
        _log.LogInformation("");
    }
}
