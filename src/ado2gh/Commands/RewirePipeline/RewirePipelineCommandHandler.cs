using System;
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

        var pipelineTestArgs = new PipelineTestArgs
        {
            AdoOrg = args.AdoOrg,
            AdoTeamProject = args.AdoTeamProject,
            PipelineName = args.AdoPipeline,
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
}
