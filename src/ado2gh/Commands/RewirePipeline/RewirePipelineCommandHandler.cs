using System;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;

public class RewirePipelineCommandHandler : ICommandHandler<RewirePipelineCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private readonly AdoPipelineTriggerService _pipelineTriggerService;
    private readonly PipelineTestService _pipelineTestService;

    public RewirePipelineCommandHandler(OctoLogger log, AdoApi adoApi, AdoPipelineTriggerService pipelineTriggerService)
    {
        _log = log;
        _adoApi = adoApi;
        _pipelineTriggerService = pipelineTriggerService;
        _pipelineTestService = new PipelineTestService(log, adoApi, pipelineTriggerService);
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

    private async Task HandleDryRun(RewirePipelineCommandArgs args)
    {
        _log.LogInformation("Starting dry-run mode: Testing pipeline rewiring to GitHub...");
        _log.LogInformation($"Monitor timeout: {args.MonitorTimeoutMinutes} minutes");

        var pipelineTestArgs = new PipelineTestArgs
        {
            AdoOrg = args.AdoOrg,
            AdoTeamProject = args.AdoTeamProject,
            PipelineName = args.AdoPipeline,
            PipelineId = args.AdoPipelineId,
            GithubOrg = args.GithubOrg,
            GithubRepo = args.GithubRepo,
            ServiceConnectionId = args.ServiceConnectionId,
            MonitorTimeoutMinutes = args.MonitorTimeoutMinutes,
            TargetApiUrl = args.TargetApiUrl
        };

        var testResult = await _pipelineTestService.TestPipeline(pipelineTestArgs);

        // Log the test result summary
        _log.LogInformation($"=== PIPELINE TEST REPORT ===");
        _log.LogInformation($"ADO Organization: {testResult.AdoOrg}");
        _log.LogInformation($"ADO Team Project: {testResult.AdoTeamProject}");
        _log.LogInformation($"Pipeline Name: {testResult.PipelineName}");
        _log.LogInformation($"Build Result: {testResult.Result ?? "not completed"}");

        if (testResult.Result == "succeeded")
        {
            _log.LogSuccess("✅ Pipeline test PASSED - Build completed successfully");
        }
        else if (testResult.Result == "failed")
        {
            _log.LogError("❌ Pipeline test FAILED - Build completed with failures");
        }
        else if (!string.IsNullOrEmpty(testResult.ErrorMessage))
        {
            _log.LogError($"❌ Pipeline test FAILED - Error: {testResult.ErrorMessage}");
        }
        else
        {
            _log.LogWarning("⚠️ Pipeline test completed with unknown result");
        }
    }

    private async Task HandleRegularRewire(RewirePipelineCommandArgs args)
    {
        _log.LogInformation($"Rewiring Pipeline to GitHub repo...");

        try
        {
            var adoPipelineId = await GetPipelineId(args);
            var (defaultBranch, clean, checkoutSubmodules, triggers) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);

            // Use the specialized service for complex trigger logic
            var rewired = await _pipelineTriggerService.RewirePipelineToGitHub(
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

            if (rewired)
            {
                _log.LogSuccess("Successfully rewired pipeline");
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            // Pipeline not found - log error and fail gracefully
            _log.LogError($"Pipeline not found: {ex.Message}");
            throw new OctoshiftCliException($"Pipeline could not be found. Please verify the pipeline name or ID and try again.");
        }
        catch (ArgumentException ex) when (ex.ParamName == "pipeline")
        {
            // Pipeline lookup failed - log error and fail gracefully
            _log.LogError($"Pipeline lookup failed: {ex.Message}");
            throw new OctoshiftCliException($"Unable to find the specified pipeline. Please verify the pipeline name and try again.");
        }
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
}
