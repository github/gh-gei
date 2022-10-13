using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class ShareServiceConnectionCommandHandler : ICommandHandler<ShareServiceConnectionCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;

    public ShareServiceConnectionCommandHandler(OctoLogger log, AdoApi adoApi)
    {
        _log = log;
        _adoApi = adoApi;
    }

    public async Task Handle(ShareServiceConnectionCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Sharing Service Connection...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"SERVICE CONNECTION ID: {args.ServiceConnectionId}");
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);

        var adoTeamProjectId = await _adoApi.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);

        if (await _adoApi.ContainsServiceConnection(args.AdoOrg, args.AdoTeamProject, args.ServiceConnectionId))
        {
            _log.LogInformation("Service connection already shared with team project");
            return;
        }

        await _adoApi.ShareServiceConnection(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, args.ServiceConnectionId);

        _log.LogSuccess("Successfully shared service connection");
    }
}
