using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards;

public class IntegrateBoardsCommandHandler : ICommandHandler<IntegrateBoardsCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public IntegrateBoardsCommandHandler(OctoLogger log, AdoApi adoApi, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _adoApi = adoApi;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public async Task Handle(IntegrateBoardsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Integrating Azure Boards...");

        args.GithubPat ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();

        var adoTeamProjectId = await _adoApi.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);
        var githubHandle = await _adoApi.GetGithubHandle(args.AdoOrg, args.AdoTeamProject, args.GithubPat);

        var boardsConnection = await _adoApi.GetBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject);

        if (boardsConnection == default)
        {
            var endpointId = await _adoApi.CreateBoardsGithubEndpoint(args.AdoOrg, adoTeamProjectId, args.GithubPat, githubHandle, Guid.NewGuid().ToString());
            var repoId = await _adoApi.GetBoardsGithubRepoId(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, endpointId, args.GithubOrg, args.GithubRepo);
            await _adoApi.CreateBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject, endpointId, repoId);
            _log.LogSuccess("Successfully configured Boards<->GitHub integration");
        }
        else
        {
            var repoId = await _adoApi.GetBoardsGithubRepoId(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, boardsConnection.endpointId, args.GithubOrg, args.GithubRepo);

            if (boardsConnection.repoIds.Any(x => x == repoId))
            {
                _log.LogWarning($"This repo is already configured in the Boards integration (Repo ID: {repoId})");
            }
            else
            {
                var repos = new List<string>(boardsConnection.repoIds)
                {
                    repoId
                };

                await _adoApi.AddRepoToBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject, boardsConnection.connectionId, boardsConnection.connectionName, boardsConnection.endpointId, repos);
                _log.LogSuccess("Successfully configured Boards<->GitHub integration");
            }
        }
    }
}
