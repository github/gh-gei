using System;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.AddTeamToRepo;

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

        _log.LogInformation("Adding team to repo...");

        var teamSlug = await _githubApi.GetTeamSlug(args.GithubOrg, args.Team);
        await _githubApi.AddTeamToRepo(args.GithubOrg, args.GithubRepo, teamSlug, args.Role);

        _log.LogSuccess("Successfully added team to repo");
    }
}
