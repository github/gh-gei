﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

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

        _log.Verbose = args.Verbose;

        _log.LogInformation("Integrating Azure Boards...");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }
        if (args.GithubPat is not null)
        {
            _log.LogInformation("GITHUB PAT: ***");
        }

        _log.RegisterSecret(args.AdoPat);
        _log.RegisterSecret(args.GithubPat);

        args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();

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
