using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    public class ConfigureAutoLinkCommand : Command
    {
        private GithubApi _github;

        public ConfigureAutoLinkCommand() : base("configure-auto-link")
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

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoOrg);
            AddOption(adoTeamProject);

            Handler = CommandHandler.Create<string, string, string, string>(Invoke);
        }

        private async Task Invoke(string githubOrg, string githubRepo, string adoOrg, string adoTeamProject)
        {
            Console.WriteLine("Configuring Autolink Reference...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _github = new GithubApi(githubToken);

            await _github.AddAutoLink(githubOrg, githubRepo, adoOrg, adoTeamProject);
        }
    }
}
