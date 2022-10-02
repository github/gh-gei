using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class DisableRepoCommandHandler
{
    private readonly OctoLogger _log;
    private readonly AdoApiFactory _adoApiFactory;

    public DisableRepoCommandHandler(OctoLogger log, AdoApiFactory adoApiFactory)
    {
        _log = log;
        _adoApiFactory = adoApiFactory;
    }

    public async Task Invoke(DisableRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Disabling repo...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"ADO REPO: {args.AdoRepo}");
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);

        var ado = _adoApiFactory.Create(args.AdoPat);

        var allRepos = await ado.GetRepos(args.AdoOrg, args.AdoTeamProject);
        if (allRepos.Any(r => r.Name == args.AdoRepo && r.IsDisabled))
        {
            _log.LogSuccess($"Repo '{args.AdoOrg}/{args.AdoTeamProject}/{args.AdoRepo}' is already disabled - No action will be performed");
            return;
        }
        var repoId = allRepos.First(r => r.Name == args.AdoRepo).Id;
        await ado.DisableRepo(args.AdoOrg, args.AdoTeamProject, repoId);

        _log.LogSuccess("Repo successfully disabled");
    }
}
