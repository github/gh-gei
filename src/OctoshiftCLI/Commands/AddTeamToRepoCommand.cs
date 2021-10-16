using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class AddTeamToRepoCommand : Command
    {
        private GithubApi _github;

        public AddTeamToRepoCommand() : base("add-team-to-repo")
        {
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var team = new Option<string>("--team")
            {
                IsRequired = true
            };
            var role = new Option<string>("--role")
            {
                IsRequired = true
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role);

            Handler = CommandHandler.Create<string, string, string, string>(Invoke);
        }

        private async Task Invoke(string githubOrg, string githubRepo, string team, string role)
        {
            Console.WriteLine("Adding team to repo...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");
            Console.WriteLine($"TEAM: {team}");
            Console.WriteLine($"ROLE: {role}");

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _github = new GithubApi(githubToken);

            await _github.AddTeamToRepo(githubOrg, githubRepo, team, role);
        }
    }
}
