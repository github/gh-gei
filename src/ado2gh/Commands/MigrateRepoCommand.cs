using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.AdoToGithub.Handlers;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class MigrateRepoCommand : Command
    {
        public MigrateRepoCommand(OctoLogger log, GithubApiFactory githubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "migrate-repo",
            description: "Invokes the GitHub API's to migrate the repo and all PR data" +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var targetRepoVisibility = new Option<string>("--target-repo-visibility")
            {
                IsRequired = false,
                Description = "Defaults to private. Valid values are public, private, internal"
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the repo migration to finish."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(targetRepoVisibility);
            AddOption(wait);
            AddOption(adoPat);
            AddOption(githubPat);
            AddOption(verbose);

            var handler = new MigrateRepoCommandHandler(log, githubApiFactory, environmentVariableProvider);
            Handler = CommandHandler.Create<MigrateRepoCommandArgs>(handler.Invoke);
        }
    }

    public class MigrateRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string TargetRepoVisibility { get; set; }
        public bool Wait { get; set; }
        public string AdoPat { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
