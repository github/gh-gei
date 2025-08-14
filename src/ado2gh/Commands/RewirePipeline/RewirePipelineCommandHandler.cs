using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;

public class RewirePipelineCommandHandler : ICommandHandler<RewirePipelineCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
<<<<<<< HEAD
    private readonly PipelineTestService _pipelineTestService;
=======
    private readonly AdoPipelineTriggerService _pipelineTriggerService;
>>>>>>> origin/main

    public RewirePipelineCommandHandler(OctoLogger log, AdoApi adoApi, AdoPipelineTriggerService pipelineTriggerService)
    {
        _log = log;
        _adoApi = adoApi;
<<<<<<< HEAD
        _pipelineTestService = new PipelineTestService(log, adoApi);
=======
        _pipelineTriggerService = pipelineTriggerService;
>>>>>>> origin/main
    }

    public async Task Handle(RewirePipelineCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
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

        // Use AdoApi for simple API calls
        var adoPipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
        var (defaultBranch, clean, checkoutSubmodules, triggers) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);

        // Use the specialized service for complex trigger logic
        await _pipelineTriggerService.RewirePipelineToGitHub(
            args.AdoOrg,
            args.AdoTeamProject,
            adoPipelineId,
            defaultBranch,
            clean,
            checkoutSubmodules,
            args.GithubOrg,
            args.GithubRepo,
            args.ServiceConnectionId,
            triggers,
            args.TargetApiUrl);

        _log.LogSuccess("Successfully rewired pipeline");
    }

    private async Task HandleDryRun(RewirePipelineCommandArgs args)
    {
        _log.LogInformation("Starting dry-run mode: Testing pipeline rewiring to GitHub...");

        var testArgs = new PipelineTestArgs
        {
            AdoOrg = args.AdoOrg,
            AdoTeamProject = args.AdoTeamProject,
            PipelineName = args.AdoPipeline,
            GithubOrg = args.GithubOrg,
            GithubRepo = args.GithubRepo,
            ServiceConnectionId = args.ServiceConnectionId,
            TargetApiUrl = args.TargetApiUrl,
            MonitorTimeoutMinutes = args.MonitorTimeoutMinutes
        };

        try
        {
            // Step 1: Get pipeline information and store original configuration
            _log.LogInformation("Step 1: Retrieving pipeline information...");
            var adoPipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
            testArgs.PipelineId = adoPipelineId;

            _log.LogInformation($"Pipeline ID: {adoPipelineId}");

            // Get original repository information for display
            var (originalRepoName, _, originalDefaultBranch, _, _) = await _adoApi.GetPipelineRepository(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
            var (defaultBranch, _, _, _) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);

            _log.LogInformation($"Original ADO Repository: {originalRepoName}");
            _log.LogInformation($"Default branch: {defaultBranch}");

            // Step 2: Rewire to GitHub
            _log.LogInformation("Step 2: Temporarily rewiring pipeline to GitHub...");

            // Step 3: Queue a build
            _log.LogInformation("Step 3: Queuing a test build...");

            // Step 4: Restore to ADO immediately after queuing build
            _log.LogInformation("Step 4: Restoring pipeline back to original ADO repository...");

            // Step 5: Monitor build progress
            _log.LogInformation($"Step 5: Monitoring build progress (timeout: {args.MonitorTimeoutMinutes} minutes)...");

            // Use the shared service to perform the actual test
            var testResult = await _pipelineTestService.TestPipeline(testArgs);

            // Log detailed status messages
            if (testResult.RewiredSuccessfully)
            {
                _log.LogSuccess("Pipeline successfully rewired to GitHub");
            }

            if (testResult.BuildId.HasValue)
            {
                _log.LogInformation($"Build queued with ID: {testResult.BuildId}");
                _log.LogInformation($"Build URL: {testResult.BuildUrl}");
            }

            if (testResult.RestoredSuccessfully)
            {
                _log.LogSuccess("Pipeline successfully restored to original ADO repository");
            }
            else if (!string.IsNullOrEmpty(testResult.ErrorMessage) && testResult.ErrorMessage.Contains("Failed to restore"))
            {
                _log.LogError($"Failed to restore pipeline to ADO: {testResult.ErrorMessage}");
                _log.LogError("MANUAL RESTORATION REQUIRED:");
                _log.LogError($"  Pipeline ID: {testResult.PipelineId}");
                _log.LogError($"  Original Repository: {originalRepoName}");
                _log.LogError($"  Pipeline URL: {testResult.PipelineUrl}");
            }

            _log.LogInformation("Monitoring build progress...");
            if (!string.IsNullOrEmpty(testResult.Result))
            {
                _log.LogInformation($"Build completed with result: {testResult.Result}");
            }
            else if (testResult.Status == "timedOut")
            {
                _log.LogWarning($"Build monitoring timed out after {args.MonitorTimeoutMinutes} minutes");
            }

            // Generate report
            GenerateReport(testResult);

            if (!testResult.RestoredSuccessfully)
            {
                throw new OctoshiftCliException("Pipeline was not properly restored to ADO repository. Manual intervention may be required.");
            }
        }
        catch (OctoshiftCliException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError($"Dry-run failed: {ex.Message}");
            throw new OctoshiftCliException($"Pipeline dry-run test failed: {ex.Message}", ex);
        }
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
