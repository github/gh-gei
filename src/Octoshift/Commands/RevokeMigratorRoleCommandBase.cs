using System.CommandLine;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommandBase : Command
{
    protected RevokeMigratorRoleCommandHandler BaseHandler { get; init; }

    public RevokeMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "revoke-migrator-role",
        description: "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.")
    {
        BaseHandler = new RevokeMigratorRoleCommandHandler(log, githubApiFactory);
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    protected virtual Option<string> Actor { get; } = new("--actor") { IsRequired = true };

    protected virtual Option<string> ActorType { get; } = new("--actor-type") { IsRequired = true };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat") { IsRequired = false };

    protected virtual Option<bool> Verbose { get; } = new("--verbose") { IsRequired = false };

    protected virtual Option<string> GhesApiUrl { get; } = new("--ghes-api-url") { IsRequired = false };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Actor);
        AddOption(ActorType);
        AddOption(GithubPat);
        AddOption(Verbose);
        AddOption(GhesApiUrl);
    }
}

public class RevokeMigratorRoleArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
    public string GhesApiUrl{ get; set; }
}
