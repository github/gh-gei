using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class IntegrateBoardsCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public IntegrateBoardsCommand(
            OctoLogger log,
            AdoApiFactory adoApiFactory,
            EnvironmentVariableProvider environmentVariableProvider) : base("integrate-boards")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Configures the Azure Boards<->GitHub integration in Azure DevOps.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.";

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(adoPat);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string githubOrg, string githubRepo, string adoPat = null, string githubPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Integrating Azure Boards...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");
            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var ado = _adoApiFactory.Create(adoPat, Name);
            githubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();

            var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var githubHandle = await ado.GetGithubHandle(adoOrg, adoTeamProject, githubPat);

            var boardsConnection = await ado.GetBoardsGithubConnection(adoOrg, adoTeamProject);

            if (boardsConnection == default)
            {
                var endpointId = await ado.CreateBoardsGithubEndpoint(adoOrg, adoTeamProjectId, githubPat, githubHandle, Guid.NewGuid().ToString());
                var repoId = await ado.GetBoardsGithubRepoId(adoOrg, adoTeamProject, adoTeamProjectId, endpointId, githubOrg, githubRepo);
                await ado.CreateBoardsGithubConnection(adoOrg, adoTeamProject, endpointId, repoId);
                _log.LogSuccess("Successfully configured Boards<->GitHub integration");
            }
            else
            {
                var repoId = await ado.GetBoardsGithubRepoId(adoOrg, adoTeamProject, adoTeamProjectId, boardsConnection.endpointId, githubOrg, githubRepo);

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

                    await ado.AddRepoToBoardsGithubConnection(adoOrg, adoTeamProject, boardsConnection.connectionId, boardsConnection.connectionName, boardsConnection.endpointId, repos);
                    _log.LogSuccess("Successfully configured Boards<->GitHub integration");
                }
            }
        }
    }
}
