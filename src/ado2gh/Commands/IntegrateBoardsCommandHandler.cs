using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class IntegrateBoardsCommandHandler
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public IntegrateBoardsCommandHandler(OctoLogger log, AdoApiFactory adoApiFactory, EnvironmentVariableProvider environmentVariableProvider)
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public async Task Invoke(IntegrateBoardsCommandArgs args)
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

            var ado = _adoApiFactory.Create(args.AdoPat);
            args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();

            var adoTeamProjectId = await ado.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);
            var githubHandle = await ado.GetGithubHandle(args.AdoOrg, args.AdoTeamProject, args.GithubPat);

            var boardsConnection = await ado.GetBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject);

            if (boardsConnection == default)
            {
                var endpointId = await ado.CreateBoardsGithubEndpoint(args.AdoOrg, adoTeamProjectId, args.GithubPat, githubHandle, Guid.NewGuid().ToString());
                var repoId = await ado.GetBoardsGithubRepoId(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, endpointId, args.GithubOrg, args.GithubRepo);
                await ado.CreateBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject, endpointId, repoId);
                _log.LogSuccess("Successfully configured Boards<->GitHub integration");
            }
            else
            {
                var repoId = await ado.GetBoardsGithubRepoId(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, boardsConnection.endpointId, args.GithubOrg, args.GithubRepo);

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

                    await ado.AddRepoToBoardsGithubConnection(args.AdoOrg, args.AdoTeamProject, boardsConnection.connectionId, boardsConnection.connectionName, boardsConnection.endpointId, repos);
                    _log.LogSuccess("Successfully configured Boards<->GitHub integration");
                }
            }
        }
    }
}
