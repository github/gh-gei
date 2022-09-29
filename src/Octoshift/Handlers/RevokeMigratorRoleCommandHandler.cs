using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Handlers;

public class RevokeMigratorRoleCommandHandler
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public RevokeMigratorRoleCommandHandler(OctoLogger log, ITargetGithubApiFactory githubApiFactory)
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
    }

    public async Task Handle(RevokeMigratorRoleArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"ACTOR: {args.Actor}");

        if (args.GithubPat is not null)
        {
            _log.LogInformation($"GITHUB PAT: ***");
        }

        args.ActorType = args.ActorType?.ToUpper();
        _log.LogInformation($"ACTOR TYPE: {args.ActorType}");

        args.ActorType = args.ActorType.ToUpper();

        if (args.ActorType is "TEAM" or "USER")
        {
            _log.LogInformation("Actor type is valid...");
        }
        else
        {
            _log.LogError("Actor type must be either TEAM or USER.");
            return;
        }

        _log.RegisterSecret(args.GithubPat);

        var githubApi = _githubApiFactory.Create(args.GhesApiUrl, args.GithubPat);
        var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
        var success = await githubApi.RevokeMigratorRole(githubOrgId, args.Actor, args.ActorType);

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
