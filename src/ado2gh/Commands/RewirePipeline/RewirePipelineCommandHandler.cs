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

    public RewirePipelineCommandHandler(OctoLogger log, AdoApi adoApi, AdoPipelineTriggerService pipelineTriggerService)
    {
        _log = log;
        _adoApi = adoApi;
        _pipelineTriggerService = pipelineTriggerService;
    }

    public async Task Handle(RewirePipelineCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

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
