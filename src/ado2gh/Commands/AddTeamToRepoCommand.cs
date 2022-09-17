using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public sealed class AddTeamToRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public AddTeamToRepoCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base(
            name: "add-team-to-repo",
            description: "Adds a team to a repo with a specific role/permission" +
                         Environment.NewLine +
                         "Note: Expects GH_PAT env variable or --github-pat option to be set.")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

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
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(team);
            AddOption(role.FromAmong("pull", "push", "admin", "maintain", "triage"));
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<AddTeamToRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(AddTeamToRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Adding team to repo...");
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
            _log.LogInformation($"TEAM: {args.Team}");
            _log.LogInformation($"ROLE: {args.Role}");
            if (args.GithubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var github = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);
            var teamSlug = await github.GetTeamSlug(args.GithubOrg, args.Team);
            await github.AddTeamToRepo(args.GithubOrg, args.GithubRepo, teamSlug, args.Role);

            _log.LogSuccess("Successfully added team to repo");
        }
    }

    public class AddTeamToRepoCommandArgs
    {
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string Team { get; set; }
        public string Role { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
