using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class CreateTeamCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApi _githubApi;

        public CreateTeamCommand(OctoLogger log, GithubApi githubApi) : base("create-team")
        {
            _log = log;
            _githubApi = githubApi;

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
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(teamName);
            AddOption(idpGroup);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string teamName, string idpGroup, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Creating GitHub team...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"TEAM NAME: {teamName}");
            _log.LogInformation($"IDP GROUP: {idpGroup}");

            await _githubApi.CreateTeam(githubOrg, teamName);

            _log.LogSuccess("Successfully created team");

            if (string.IsNullOrWhiteSpace(idpGroup))
            {
                _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
            }
            else
            {
                var members = await _githubApi.GetTeamMembers(githubOrg, teamName);

                foreach (var member in members)
                {
                    await _githubApi.RemoveTeamMember(githubOrg, teamName, member);
                }

                var idpGroupId = await _githubApi.GetIdpGroupId(githubOrg, idpGroup);
                var teamSlug = await _githubApi.GetTeamSlug(githubOrg, teamName);

                await _githubApi.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId);

                _log.LogSuccess("Successfully linked team to Idp group");
            }
        }
    }
}