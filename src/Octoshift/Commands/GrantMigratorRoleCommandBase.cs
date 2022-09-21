using System;
using System.CommandLine;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class GrantMigratorRoleCommandBase : Command
{
    protected GrantMigratorRoleCommandBaseHandler BaseHandler { get; init; }

    public GrantMigratorRoleCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base(
        name: "grant-migrator-role",
        description: "Allows an organization admin to grant a USER or TEAM the migrator role for a single GitHub organization. The migrator role allows the role assignee to perform migrations into the target organization.")
    {
        BaseHandler = new GrantMigratorRoleCommandBaseHandler(log, githubApiFactory);
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
}

public class GrantMigratorRoleCommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
