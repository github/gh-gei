using System;
using System.CommandLine;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class GrantMigratorRoleCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;

    public GrantMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "grant-migrator-role",
        description: "Allows an organization admin to grant a USER or TEAM the migrator role for a single GitHub organization. The migrator role allows the role assignee to perform migrations into the target organization.")
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

    public async Task Handle(GrantMigratorRoleCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Granting migrator role ...");
        _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {args.GithubOrg}");
        _log.LogInformation($"{Actor.GetLogFriendlyName()}: {args.Actor}");

        args.ActorType = args.ActorType?.ToUpper();
        _log.LogInformation($"{ActorType.GetLogFriendlyName()}: {args.ActorType}");

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
            _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
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

public class GrantMigratorRoleCommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
