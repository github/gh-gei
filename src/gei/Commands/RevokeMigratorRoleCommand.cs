using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class RevokeMigratorRoleCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _githubApiFactory;

        public RevokeMigratorRoleCommand(OctoLogger log, ITargetGithubApiFactory githubApiFactory) : base("revoke-migrator-role")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            Description = "Allows an organization admin to revoke the migrator role for a USER or TEAM for a single GitHub organization. This will remove their ability to run a migration into the target organization.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-target-pat option to be set.";

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
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(actor);
            AddOption(actorType);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string actor, string actorType, string githubTargetPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Granting migrator role ...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"ACTOR: {actor}");
            if (githubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }

            actorType = actorType?.ToUpper();
            _log.LogInformation($"ACTOR TYPE: {actorType}");

            actorType = actorType.ToUpper();

            if (actorType is "TEAM" or "USER")
            {
                _log.LogInformation("Actor type is valid...");
            }
            else
            {
                _log.LogError("Actor type must be either TEAM or USER.");
                return;
            }

            var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubTargetPat);
            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            var success = await githubApi.RevokeMigratorRole(githubOrgId, actor, actorType);

            if (success)
            {
                _log.LogSuccess($"Migrator role successfully revoked for the {actorType} \"{actor}\"");
            }
            else
            {
                _log.LogError($"Migrator role couldn't be revoked for the {actorType} \"{actor}\"");
            }
        }
    }
}
