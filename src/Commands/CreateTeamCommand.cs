using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class CreateTeamCommand : Command
    {
        private GithubApi _github;

        public CreateTeamCommand() : base("create-team")
        {
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

        private async Task Invoke(string githubOrg, string teamName, string idpGroupName)
        {
            Console.WriteLine("Creating GitHub team...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"TEAM NAME: {teamName}");
            Console.WriteLine($"IDP GROUP: {idpGroupName}");

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _github = new GithubApi(githubToken);

            await _github.CreateTeam(githubOrg, teamName);
            var members = await _github.GetTeamMembers(githubOrg, teamName);

            foreach (var member in members)
            {
                await _github.RemoveTeamMember(githubOrg, teamName, member);
            }

            if (string.IsNullOrWhiteSpace(idpGroupName))
            {
                Console.WriteLine("No IdP Group provided, skipping the IdP linking step");
            }
            else
            {
                var idpGroup = await _github.GetIdpGroup(githubOrg, idpGroupName);

                await _github.AddTeamSync(githubOrg, teamName, idpGroup.id, idpGroup.name, idpGroup.description);
            }
        }
    }
}
