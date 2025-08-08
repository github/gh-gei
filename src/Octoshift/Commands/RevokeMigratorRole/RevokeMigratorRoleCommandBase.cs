using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.RevokeMigratorRole;

public class RevokeMigratorRoleCommandBase : CommandBase<RevokeMigratorRoleCommandArgs, RevokeMigratorRoleCommandHandler>
{
    public RevokeMigratorRoleCommandBase() : base(
        name: "revoke-migrator-role",
        description: "Allows an organization owner to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.")
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
        var apiUrl = args.TargetApiUrl ?? args.GhesApiUrl;
        var githubApi = githubApiFactory.Create(apiUrl, null, args.GithubPat);

        return new RevokeMigratorRoleCommandHandler(log, githubApi);
    }

    public virtual Option<string> GhesApiUrl { get; } = new("--ghes-api-url")
    {
        IsRequired = false,
        Description = "The URL of the GitHub Enterprise Server instance, if migrating from GHES. Supports revoking access for exports. Can only configure one of --ghes-api-url or --target-api-url at a time."
    };
    public virtual Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        IsRequired = false,
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com. Can only configure one of --ghes-api-url or --target-api-url at a time."
    };
    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Actor);
        AddOption(ActorType);
        AddOption(GithubPat);
        AddOption(Verbose);
        AddOption(GhesApiUrl);
        AddOption(TargetApiUrl);
    }
}
