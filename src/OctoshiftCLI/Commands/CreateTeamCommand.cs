using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class CreateTeamCommand : Command
    {
        public CreateTeamCommand() : base("create-team")
        {
            Description = "Creates a GitHub team and optionally links it to an IdP group.";

            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var teamName = new Option<string>("--team-name")
            {
                IsRequired = true
            };
            var idpGroup = new Option<string>("--idp-group")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(teamName);
            AddOption(idpGroup);

            Handler = CommandHandler.Create<string, string, string>(Invoke);
        }

        private async Task Invoke(string githubOrg, string teamName, string idpGroup)
        {
            Console.WriteLine("Creating GitHub team...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"TEAM NAME: {teamName}");
            Console.WriteLine($"IDP GROUP: {idpGroup}");

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            using var github = new GithubApi(githubToken);

            await github.CreateTeam(githubOrg, teamName);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully created team");
            Console.ResetColor();

            if (string.IsNullOrWhiteSpace(idpGroup))
            {
                Console.WriteLine("No IdP Group provided, skipping the IdP linking step");
            }
            else
            {
                var members = await github.GetTeamMembers(githubOrg, teamName);

                foreach (var member in members)
                {
                    await github.RemoveTeamMember(githubOrg, teamName, member);
                }

                var idpGroupId = await github.GetIdpGroupId(githubOrg, idpGroup);
                var teamSlug = await github.GetTeamSlug(githubOrg, teamName);

                await github.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Successfully linked team to Idp group");
                Console.ResetColor();
            }
        }
    }
}
