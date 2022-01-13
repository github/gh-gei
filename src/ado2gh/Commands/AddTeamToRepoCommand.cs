using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class AddTeamToRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly Lazy<GithubApi> _lazyGithubApi;

        public AddTeamToRepoCommand(OctoLogger log, Lazy<GithubApi> lazyGithubApi) : base("add-team-to-repo")
        {
            _log = log;
            _lazyGithubApi = lazyGithubApi;

            Description = "Adds a team to a repo with a specific role/permission";

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
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role.FromAmong("pull", "push", "admin", "maintain", "triage"));
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string team, string role, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Adding team to repo...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"TEAM: {team}");
            _log.LogInformation($"ROLE: {role}");

            await _lazyGithubApi.Value.AddTeamToRepo(githubOrg, githubRepo, team, role);

            _log.LogSuccess("Successfully added team to repo");
        }
    }
}