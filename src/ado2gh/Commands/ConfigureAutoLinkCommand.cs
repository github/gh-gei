using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ConfigureAutoLinkCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public ConfigureAutoLinkCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("configure-autolink")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable to be set.";

            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Configuring Autolink Reference...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");

            // TODO: This crashes if autolink is already configured
            await _githubApiFactory.Create().AddAutoLink(githubOrg, githubRepo, adoOrg, adoTeamProject);

            _log.LogSuccess("Successfully configured autolink references");
        }
    }
}