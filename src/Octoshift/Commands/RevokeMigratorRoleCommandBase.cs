using System;
using System.CommandLine;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public RevokeMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "revoke-migrator-role",
        description: "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    protected virtual Option<string> Actor { get; } = new("--actor") { IsRequired = true };

    protected virtual Option<string> ActorType { get; } = new("--actor-type") { IsRequired = true };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Actor);
        AddOption(ActorType);
        AddOption(GithubPat);
        AddOption(Verbose);
    }

    public async Task Handle(RevokeMigratorRoleArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {args.GithubOrg}");
        _log.LogInformation($"{Actor.GetLogFriendlyName()}: {args.Actor}");

        if (args.GithubPat is not null)
        {
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
        }

        args.ActorType = args.ActorType?.ToUpper();
        _log.LogInformation($"{ActorType.GetLogFriendlyName()}: {args.ActorType}");

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

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);
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

public class RevokeMigratorRoleArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
