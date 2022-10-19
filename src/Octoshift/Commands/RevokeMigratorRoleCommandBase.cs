using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public class RevokeMigratorRoleCommandBase : CommandBase<RevokeMigratorRoleCommandArgs, RevokeMigratorRoleCommandHandler>
{
    public RevokeMigratorRoleCommandBase() : base(
        name: "revoke-migrator-role",
        description: "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.")
    {
    }

    public virtual Option<string> GithubOrg { get; } = new("--github-org") { IsRequired = true };

    public virtual Option<string> Actor { get; } = new("--actor") { IsRequired = true };

    public virtual Option<string> ActorType { get; } = new("--actor-type") { IsRequired = true };

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose");

    public override RevokeMigratorRoleCommandHandler BuildHandler(RevokeMigratorRoleCommandArgs args, IServiceProvider sp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (sp is null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        var log = sp.GetRequiredService<OctoLogger>();
        var githubApiFactory = sp.GetRequiredService<ITargetGithubApiFactory>();
        var githubApi = githubApiFactory.Create(args.GhesApiUrl, args.GithubPat);

        return new RevokeMigratorRoleCommandHandler(log, githubApi);
    }

    public virtual Option<string> GhesApiUrl { get; } = new("--ghes-api-url") { IsRequired = false };

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

public class RevokeMigratorRoleCommandArgs
{
    public string GithubOrg { get; set; }
    public string Actor { get; set; }
    public string ActorType { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
    public string GhesApiUrl { get; set; }
}
