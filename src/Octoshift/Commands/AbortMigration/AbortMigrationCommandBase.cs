using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;


namespace OctoshiftCLI.Commands.AbortMigration;

public class AbortMigrationCommandBase : CommandBase<AbortMigrationCommandArgs, AbortMigrationCommandHandler>
{
    public AbortMigrationCommandBase() : base(
        name: "abort-migration",
        description: "Aborts a repository migration that is queued or in progress.")
    {
    }

    public virtual Option<string> MigrationId { get; } = new("--migration-id")
    {
        IsRequired = true,
        Description = "The ID of the migration to abort, starting with RM_. Organization migrations, where the ID starts with OM_, are not supported."
    };

    public virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        Description = "Personal access token of the GitHub target. Overrides GH_PAT environment variable."
    };

    public virtual Option<bool> Verbose { get; } = new("--verbose")
    {
        Description = "Display more information to the console."
    };
    public virtual Option<string> TargetApiUrl { get; } = new("--target-api-url")
    {
        Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
    };
    protected void AddOptions()
    {
        AddOption(MigrationId);
        AddOption(GithubPat);
        AddOption(Verbose);
        AddOption(TargetApiUrl);
    }

    public override AbortMigrationCommandHandler BuildHandler(AbortMigrationCommandArgs args, IServiceProvider sp)
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
        var githubApi = sp.GetRequiredService<ITargetGithubApiFactory>().Create(args.TargetApiUrl, args.GithubPat);

        return new AbortMigrationCommandHandler(log, githubApi);
    }
}
