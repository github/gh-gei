using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommand : CommandBase<MigrateRepoCommandArgs, MigrateRepoCommandHandler>
    {
        public MigrateRepoCommand() : base(
            name: "migrate-repo",
            description: "Invokes the GitHub API's to migrate the repo and all PR data" +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(AdoRepo);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(AdoServerUrl);
            AddOption(QueueOnly);
            AddOption(TargetRepoVisibility.FromAmong("public", "private", "internal"));
            AddOption(AdoPat);
            AddOption(GithubPat);
            AddOption(Verbose);
            AddOption(TargetApiUrl);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true
        };
        public Option<string> AdoRepo { get; } = new("--ado-repo")
        {
            IsRequired = true
        };
        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true
        };
        public Option<string> GithubRepo { get; } = new("--github-repo")
        {
            IsRequired = true
        };
        public Option<string> AdoServerUrl { get; } = new("--ado-server-url")
        {
            IsHidden = true,
            Description = "Required if migrating from ADO Server. E.g. https://myadoserver.contoso.com. When migrating from ADO Server, --ado-source-org represents the collection name."
        };
        public Option<bool> QueueOnly { get; } = new("--queue-only")
        {
            Description = "Only queues the migration, does not wait for it to finish. Use the wait-for-migration command to subsequently wait for it to finish and view the status."
        };
        public Option<string> TargetRepoVisibility { get; } = new("--target-repo-visibility")
        {
            Description = "The visibility of the target repo. Defaults to private. Valid values are public, private, or internal."
        };
        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<string> GithubPat { get; } = new("--github-pat");
        public Option<bool> Verbose { get; } = new("--verbose");

        public override MigrateRepoCommandHandler BuildHandler(MigrateRepoCommandArgs args, IServiceProvider sp)
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
            var githubApi = githubApiFactory.Create(args.TargetApiUrl, null, args.GithubPat);
            var environmentVariableProvider = sp.GetRequiredService<EnvironmentVariableProvider>();
            var warningsCountLogger = sp.GetRequiredService<WarningsCountLogger>();

            return new MigrateRepoCommandHandler(log, githubApi, environmentVariableProvider, warningsCountLogger);
        }
    }
}
