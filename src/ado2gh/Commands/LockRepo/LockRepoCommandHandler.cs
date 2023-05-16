using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.LockRepo;

public class LockRepoCommandHandler : ICommandHandler<LockRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;

    public LockRepoCommandHandler(OctoLogger log, AdoApi adoApi)
    {
        _log = log;
        _adoApi = adoApi;
    }

    public async Task Handle(LockRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Locking repo...");

        var teamProjectId = await _adoApi.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);
        var repoId = await _adoApi.GetRepoId(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

        var identityDescriptor = await _adoApi.GetIdentityDescriptor(args.AdoOrg, teamProjectId, "Project Valid Users");
        await _adoApi.LockRepo(args.AdoOrg, teamProjectId, repoId, identityDescriptor);

        _log.LogSuccess("Repo successfully locked");
    }
}
