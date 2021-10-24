using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class RevokeMigratorRoleCommand : Command
    {
        public RevokeMigratorRoleCommand() : base("revoke-migrator-role")
        {
            Description = "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.";

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
            actorType = actorType.ToUpper();

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            if (actorType is "TEAM" or "USER")
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

            using var github = new GithubApi(githubToken);

            var githubOrgId = await github.GetOrganizationId(githubOrg);
            var revokeMigratorRoleState = await github.RevokeMigratorRole(githubOrgId, actor, actorType);

            if (revokeMigratorRoleState.Trim().ToUpper() == "TRUE")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"SUCCESS: Migrator role successfully revoked for the {actorType} \"{actor}\"");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Migrator role couldn't be revoked for the {actorType} \"{actor}\"");
                Console.ResetColor();
            }
        }
    }
}
