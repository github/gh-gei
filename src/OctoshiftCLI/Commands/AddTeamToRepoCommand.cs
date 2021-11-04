using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class AddTeamToRepoCommand : Command
    {
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

        public async Task Invoke(string githubOrg, string githubRepo, string team, string role)
        {
            Console.WriteLine("Adding team to repo...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");
            Console.WriteLine($"TEAM: {team}");
            Console.WriteLine($"ROLE: {role}");

            using var github = GithubApiFactory.Create();

            await github.AddTeamToRepo(githubOrg, githubRepo, team, role);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully added team to repo");
            Console.ResetColor();
        }
    }
}