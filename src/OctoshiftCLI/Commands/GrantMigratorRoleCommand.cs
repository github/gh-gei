using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class GrantMigratorRoleCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApi _githubApi;

        public GrantMigratorRoleCommand(OctoLogger log, GithubApi githubApi) : base("grant-migrator-role")
        {
            _log = log;
            _githubApi = githubApi;

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
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(actor);
            AddOption(actorType);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string actor, string actorType, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Granting migrator role ...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"ACTOR: {actor}");

            actorType = actorType?.ToUpper();
            _log.LogInformation($"ACTOR TYPE: {actorType}");

            if (actorType is "TEAM" or "USER")
            {
                _log.LogInformation("Actor type is valid...");
            }
            else
            {
                _log.LogError("Actor type must be either TEAM or USER.");
                return;
            }

            var githubOrgId = await _githubApi.GetOrganizationId(githubOrg);
            var success = await _githubApi.GrantMigratorRole(githubOrgId, actor, actorType);

            if (success)
            {
                _log.LogSuccess($"Migrator role successfully set for the {actorType} \"{actor}\"");
            }
            else
            {
                _log.LogError($"Migrator role couldn't be set for the {actorType} \"{actor}\"");
            }
        }
    }
}