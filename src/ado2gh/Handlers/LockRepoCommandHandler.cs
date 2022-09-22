using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class LockRepoCommandHandler
{
    private readonly OctoLogger _log;
    private readonly AdoApiFactory _adoApiFactory;

    public LockRepoCommandHandler(OctoLogger log, AdoApiFactory adoApiFactory)
    {
        _log = log;
        _adoApiFactory = adoApiFactory;
    }

    public async Task Invoke(LockRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Locking repo...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"ADO REPO: {args.AdoRepo}");
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);

        var ado = _adoApiFactory.Create(args.AdoPat);

        var teamProjectId = await ado.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);
        var repoId = await ado.GetRepoId(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

        var identityDescriptor = await ado.GetIdentityDescriptor(args.AdoOrg, teamProjectId, "Project Valid Users");
        await ado.LockRepo(args.AdoOrg, teamProjectId, repoId, identityDescriptor);

        _log.LogSuccess("Repo successfully locked");
    }
}
