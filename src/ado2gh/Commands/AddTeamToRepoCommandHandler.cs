using System;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class AddTeamToRepoCommandHandler
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public AddTeamToRepoCommandHandler(OctoLogger log, GithubApiFactory githubApiFactory)
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
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
}
