using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class ShareServiceConnectionCommandHandler
{
    private readonly OctoLogger _log;
    private readonly AdoApiFactory _adoApiFactory;

    public ShareServiceConnectionCommandHandler(OctoLogger log, AdoApiFactory adoApiFactory)
    {
        _log = log;
        _adoApiFactory = adoApiFactory;
    }

    public async Task Invoke(ShareServiceConnectionCommandArgs args)
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

        var ado = _adoApiFactory.Create(args.AdoPat);

        var adoTeamProjectId = await ado.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);

        if (await ado.ContainsServiceConnection(args.AdoOrg, args.AdoTeamProject, args.ServiceConnectionId))
        {
            _log.LogInformation("Service connection already shared with team project");
            return;
        }

        await ado.ShareServiceConnection(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, args.ServiceConnectionId);

        _log.LogSuccess("Successfully shared service connection");
    }
}
