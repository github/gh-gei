using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
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
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Configuring Autolink Reference...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var keyPrefix = "AB#";
            var urlTemplate = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/";

            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);

            var autoLinks = await githubApi.GetAutoLinks(githubOrg, githubRepo);
            if (autoLinks.Any(al => al.KeyPrefix == keyPrefix && al.UrlTemplate == urlTemplate))
            {
                _log.LogSuccess($"Autolink reference already exists for key_prefix: '{keyPrefix}'. No operation will be performed");
                return;
            }

            var autoLink = autoLinks.FirstOrDefault(al => al.KeyPrefix == keyPrefix);
            if (autoLink != default((int, string, string)))
            {
                _log.LogInformation($"Autolink reference already exists for key_prefix: '{keyPrefix}', but the url template is incorrect");
                _log.LogInformation($"Deleting existing Autolink reference for key_prefix: '{keyPrefix}' before creating a new Autolink reference");
                await githubApi.DeleteAutoLink(githubOrg, githubRepo, autoLink.Id);
            }

            await githubApi.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate);

            _log.LogSuccess("Successfully configured autolink references");
        }
    }
}
