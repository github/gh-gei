using System;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.GrantMigratorRole;

public class GrantMigratorRoleCommandHandler : ICommandHandler<GrantMigratorRoleCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public GrantMigratorRoleCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(GrantMigratorRoleCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Granting migrator role ...");

        var githubOrgId = await _githubApi.GetOrganizationId(args.GithubOrg);
        var success = await _githubApi.GrantMigratorRole(githubOrgId, args.Actor, args.ActorType);

        if (success)
        {
            _log.LogSuccess($"Migrator role successfully set for the {args.ActorType} \"{args.Actor}\"");
        }
        else
        {
            _log.LogError($"Migrator role couldn't be set for the {args.ActorType} \"{args.Actor}\"");
        }
    }
}
