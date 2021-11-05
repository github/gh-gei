using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class AddTeamToRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubFactory;

        public AddTeamToRepoCommand(OctoLogger log, GithubApiFactory githubFactory) : base("add-team-to-repo")
        {
            _log = log;
            _githubFactory = githubFactory;

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
                IsRequired = true
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role);

            Handler = CommandHandler.Create<string, string, string, string>(Invoke);
        }

        public async Task Invoke(string githubOrg, string githubRepo, string team, string role)
        {
            _log.LogInformation("Adding team to repo...");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            _log.LogInformation($"TEAM: {team}");
            _log.LogInformation($"ROLE: {role}");

            using var github = _githubFactory.Create();

            await github.AddTeamToRepo(githubOrg, githubRepo, team, role);

            _log.LogSuccess("Successfully added team to repo");
        }
    }
}