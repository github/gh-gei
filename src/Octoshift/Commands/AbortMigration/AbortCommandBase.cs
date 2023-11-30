using System;
using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;


namespace OctoshiftCLI.Commands.AbortMigration;

public class AbortMigrationCommandBase : CommandBase<AbortMigrationCommandArgs, AbortMigrationCommandHandler>
{
    public AbortMigrationCommandBase() : base(
        name: "abort-migration",
        description: "Aborts a migration that is in progress.")
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

    public virtual Option<bool> Verbose { get; } = new("--verbose");

    protected void AddOptions()
    {
        AddOption(MigrationId);
        AddOption(GithubPat);
        AddOption(Verbose);
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
        var githubApi = sp.GetRequiredService<ITargetGithubApiFactory>().Create(targetPersonalAccessToken: args.GithubPat);

        return new AbortMigrationCommandHandler(log, githubApi);
    }
}
