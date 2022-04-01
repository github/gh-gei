using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class CreateTeamCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public CreateTeamCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("create-team")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Creates a GitHub team and optionally links it to an IdP group.";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

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
            var githubPat = new Option<string>("--github-pat")
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
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string teamName, string idpGroup, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Creating GitHub team...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"TEAM NAME: {teamName}");
            _log.LogInformation($"IDP GROUP: {idpGroup}");
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);

            var teams = await githubApi.GetTeams(githubOrg);
            if (teams.Contains(teamName))
            {
                _log.LogSuccess($"Team '{teamName}' already exists - New team will not be created");
            }
            else
            {
                await githubApi.CreateTeam(githubOrg, teamName);
                _log.LogSuccess("Successfully created team");
            }

            if (string.IsNullOrWhiteSpace(idpGroup))
            {
                _log.LogInformation("No IdP Group provided, skipping the IdP linking step");
            }
            else
            {
                var members = await githubApi.GetTeamMembers(githubOrg, teamName);

                foreach (var member in members)
                {
                    await githubApi.RemoveTeamMember(githubOrg, teamName, member);
                }

                var idpGroupId = await githubApi.GetIdpGroupId(githubOrg, idpGroup);
                var teamSlug = await githubApi.GetTeamSlug(githubOrg, teamName);

                await githubApi.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId);

                _log.LogSuccess("Successfully linked team to Idp group");
            }
        }
    }
}
