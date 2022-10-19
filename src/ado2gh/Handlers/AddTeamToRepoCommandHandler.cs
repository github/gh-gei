using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class AddTeamToRepoCommandHandler : ICommandHandler<AddTeamToRepoCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public AddTeamToRepoCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(AddTeamToRepoCommandArgs args)
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

        _log.RegisterSecret(args.GithubPat);

        var teamSlug = await _githubApi.GetTeamSlug(args.GithubOrg, args.Team);
        await _githubApi.AddTeamToRepo(args.GithubOrg, args.GithubRepo, teamSlug, args.Role);

        _log.LogSuccess("Successfully added team to repo");
    }
}
