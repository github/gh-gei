using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateOrgCommand : Command
    {
        public MigrateOrgCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "migrate-org",
            description: "Invokes the GitHub APIs to migrate a GitHub org with its teams and the repositories.")
        {
            IsHidden = true;

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
            };
            var githubTargetEnterprise = new Option<string>("--github-target-enterprise")
            {
                IsRequired = true,
                Description = "Name of the target enterprise."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the org migration to finish."
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(githubTargetOrg);
            AddOption(githubTargetEnterprise);

            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(wait);
            AddOption(verbose);

            var handler = new MigrateOrgCommandHandler(log, targetGithubApiFactory, environmentVariableProvider);
            Handler = CommandHandler.Create<MigrateOrgCommandArgs>(handler.Invoke);
        }
    }

    public class MigrateOrgCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public string GithubTargetEnterprise { get; set; }
        public bool Wait { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
    }
}
