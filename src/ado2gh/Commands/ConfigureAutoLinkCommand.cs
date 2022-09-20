using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ConfigureAutoLinkCommand : Command
    {
        public ConfigureAutoLinkCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base(
            name: "configure-autolink",
            description: "Configures Autolink References in GitHub so that references to Azure Boards work items become hyperlinks in GitHub" +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
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

            var handler = new ConfigureAutoLinkCommandHandler(log, githubApiFactory);
            Handler = CommandHandler.Create<ConfigureAutoLinkCommandArgs>(handler.Invoke);
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
