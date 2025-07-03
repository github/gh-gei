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

    public IntegrateBoardsCommandHandler(OctoLogger log, AdoApi adoApi)
    {
        _log = log;
        _adoApi = adoApi;
    }

    public async Task Handle(IntegrateBoardsCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Integrating Azure Boards with GitHub App...");

        var adoTeamProjectId = await _adoApi.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);

        // Find or use provided service connection
        var serviceConnectionId = args.ServiceConnectionId;
        if (string.IsNullOrEmpty(serviceConnectionId))
        {
            _log.LogInformation($"No service connection ID provided, searching for GitHub App service connection for org '{args.GithubOrg}'...");
            serviceConnectionId = await _adoApi.GetBoardsGithubAppServiceConnection(args.AdoOrg, args.AdoTeamProject, args.GithubOrg);
            if (string.IsNullOrEmpty(serviceConnectionId))
            {
                throw new OctoshiftCliException($"No GitHub App service connection found for GitHub org '{args.GithubOrg}' in team project '{args.AdoTeamProject}'. " +
                    "Please ensure a GitHub App service connection is configured with the name matching the GitHub org, or provide a specific service connection ID using --service-connection-id.");
            }
            _log.LogInformation($"Found GitHub App service connection: {serviceConnectionId}");
        }

        var boardsConnection = await _adoApi.GetBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject);

        if (boardsConnection == default)
        {
            var repoId = await _adoApi.GetBoardsGithubRepoId(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, serviceConnectionId, args.GithubOrg, args.GithubRepo);
            await _adoApi.CreateBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject, serviceConnectionId, repoId);
            _log.LogSuccess("Successfully configured Boards<->GitHub integration using GitHub App");
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
                _log.LogSuccess("Successfully configured Boards<->GitHub integration using GitHub App");
            }
        }
    }
}
