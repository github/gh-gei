using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class GrantMigratorRoleCommand : Command
    {
        private GithubApi _github;

        public GrantMigratorRoleCommand() : base("grant-migrator-role")
        {
            Description = "Allows an organization admin to grant a USER or TEAM the migrator role for a single GitHub organization. The migrator role allows the role assignee to perform migrations into the target organization.";

            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var actor = new Option<string>("--actor")
            {
                IsRequired = true
            };
            var actorType = new Option<string>("--actor-type")
            {
                IsRequired = true
            };
            
            AddOption(githubOrg);
            AddOption(actor);
            AddOption(actorType);

            Handler = CommandHandler.Create<string, string, string>(Invoke);
        }

        private async Task Invoke(string githubOrg, string actor, string actorType)
        {
            Console.WriteLine("Granting migrator role ...");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"ACTOR: {actor}");

            actorType = actorType.ToUpper();
            Console.WriteLine($"ACTOR TYPE: {actorType}");

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            
            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            if(actorType == "TEAM" || actorType == "USER")
            {
               Console.WriteLine("Actor type is valid...");
            } 
            else 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Actor type must be either TEAM or USER.");
                Console.ResetColor();
                return;
            }

            _github = new GithubApi(githubToken);

            var githubOrgId = await _github.GetOrganizationId(githubOrg);
            var grantMigratorRoleState = await _github.GrantMigratorRole(githubOrgId, actor, actorType);

            Console.WriteLine(grantMigratorRoleState);

            if (grantMigratorRoleState.Trim().ToUpper() == "TRUE")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"SUCCESS: Migrator role successfully set for the {actorType} \"{actor}\"");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Migrator role couldn't be set for the {actorType} \"{actor}\"");
                Console.ResetColor();
            }
        }
    }
}
