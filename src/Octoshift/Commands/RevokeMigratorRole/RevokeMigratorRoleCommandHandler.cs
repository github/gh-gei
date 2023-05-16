using System;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.RevokeMigratorRole;

public class RevokeMigratorRoleCommandHandler : ICommandHandler<RevokeMigratorRoleCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public RevokeMigratorRoleCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(RevokeMigratorRoleCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Granting migrator role ...");

        args.ActorType = args.ActorType?.ToUpper();

        if (args.ActorType is "TEAM" or "USER")
        {
            _log.LogInformation("Actor type is valid...");
        }
        else
        {
            _log.LogError("Actor type must be either TEAM or USER.");
            return;
        }

        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);
        var success = await _githubApi.RevokeMigratorRole(githubOrgId, args.Actor, args.ActorType);

        if (success)
        {
            _log.LogSuccess($"Migrator role successfully revoked for the {args.ActorType} \"{args.Actor}\"");
        }
        else
        {
            _log.LogError($"Migrator role couldn't be revoked for the {args.ActorType} \"{args.Actor}\"");
        }
    }
}
