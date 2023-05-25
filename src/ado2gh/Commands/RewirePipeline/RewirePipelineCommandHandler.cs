using System;
using System.Threading.Tasks;
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

        _log.LogInformation($"Rewiring Pipeline to GitHub repo...");

        var adoPipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
        var (defaultBranch, clean, checkoutSubmodules) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
        await _adoApi.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo, args.ServiceConnectionId);

        _log.LogSuccess("Successfully rewired pipeline");
    }
}
