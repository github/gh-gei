using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Handlers;

public class GrantMigratorRoleCommandBaseHandler
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public GrantMigratorRoleCommandBaseHandler(OctoLogger log, ITargetGithubApiFactory githubApiFactory)
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
    }
    public async Task Handle(GrantMigratorRoleCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"ACTOR: {args.Actor}");

        args.ActorType = args.ActorType?.ToUpper();
        _log.LogInformation($"ACTOR TYPE: {args.ActorType}");

        if (args.ActorType is "TEAM" or "USER")
        {
            _log.LogInformation("Actor type is valid...");
        }
        else
        {
            _log.LogError("Actor type must be either TEAM or USER.");
            return;
        }

        if (args.GithubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
        }

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);
        var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
        var success = await githubApi.GrantMigratorRole(githubOrgId, args.Actor, args.ActorType);

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
