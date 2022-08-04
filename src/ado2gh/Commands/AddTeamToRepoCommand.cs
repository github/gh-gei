using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class AddTeamToRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public AddTeamToRepoCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("add-team-to-repo")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Adds a team to a repo with a specific role/permission";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT env variable or --github-pat option to be set.";

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
                IsRequired = true,
                Description = "The only valid values are: pull, push, admin, maintain, triage. For more details see https://docs.github.com/en/rest/reference/teams#add-or-update-team-repository-permissions, custom repository roles are not currently supported."
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
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role.FromAmong("pull", "push", "admin", "maintain", "triage"));
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string team, string role, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Adding team to repo...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"TEAM: {team}");
            _log.LogInformation($"ROLE: {role}");
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var github = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);
            var teamSlug = await github.GetTeamSlug(githubOrg, team);
            await github.AddTeamToRepo(githubOrg, githubRepo, teamSlug, role);

            _log.LogSuccess("Successfully added team to repo");
        }
    }
}
