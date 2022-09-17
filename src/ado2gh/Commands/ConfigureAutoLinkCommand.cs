using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ConfigureAutoLinkCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public ConfigureAutoLinkCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base(
            name: "configure-autolink",
            description: "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub" +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

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
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<ConfigureAutoLinkCommandArgs>(Invoke);
        }

        public async Task Invoke(ConfigureAutoLinkCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Configuring Autolink Reference...");
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            if (args.GithubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var keyPrefix = "AB#";
            var urlTemplate = $"https://dev.azure.com/{args.AdoOrg}/{args.AdoTeamProject}/_workitems/edit/<num>/";

            var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

            var autoLinks = await githubApi.GetAutoLinks(args.GithubOrg, args.GithubRepo);
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
                await githubApi.DeleteAutoLink(args.GithubOrg, args.GithubRepo, autoLink.Id);
            }

            await githubApi.AddAutoLink(args.GithubOrg, args.GithubRepo, keyPrefix, urlTemplate);

            _log.LogSuccess("Successfully configured autolink references");
        }
    }

    public class ConfigureAutoLinkCommandArgs
    {
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
