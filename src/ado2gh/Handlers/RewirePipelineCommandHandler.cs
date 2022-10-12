using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

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

        _log.Verbose = args.Verbose;

        _log.LogInformation($"Rewiring Pipeline to GitHub repo...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"ADO PIPELINE: {args.AdoPipeline}");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
        _log.LogInformation($"SERVICE CONNECTION ID: {args.ServiceConnectionId}");
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);

        var adoPipelineId = await _adoApi.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
        var (defaultBranch, clean, checkoutSubmodules) = await _adoApi.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
        await _adoApi.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo, args.ServiceConnectionId);

        _log.LogSuccess("Successfully rewired pipeline");
    }
}
