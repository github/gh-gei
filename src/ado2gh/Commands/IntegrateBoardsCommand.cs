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
        private readonly Lazy<AdoApi> _lazyAdoApi;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public IntegrateBoardsCommand(
            OctoLogger log,
            Lazy<AdoApi> lazyAdoApi,
            EnvironmentVariableProvider environmentVariableProvider) : base("integrate-boards")
        {
            _log = log;
            _lazyAdoApi = lazyAdoApi;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Configures the Azure Boards<->GitHub integration in Azure DevOps.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT and GH_PAT env variables to be set.";

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
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string githubOrg, string githubRepo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Integrating Azure Boards...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPO: {githubRepo}");

            var ado = _lazyAdoApi.Value;
            var githubToken = _environmentVariableProvider.GithubPersonalAccessToken();

            var userId = await ado.GetUserId();
            var adoOrgId = await ado.GetOrganizationId(userId, adoOrg);
            var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var githubHandle = await ado.GetGithubHandle(adoOrg, adoOrgId, adoTeamProject, githubToken);

            var boardsConnection = await ado.GetBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject);

            if (boardsConnection == default)
            {
                var endpointId = await ado.CreateBoardsGithubEndpoint(adoOrg, adoTeamProjectId, githubToken, githubHandle, Guid.NewGuid().ToString());
                var repoId = await ado.GetBoardsGithubRepoId(adoOrg, adoOrgId, adoTeamProject, adoTeamProjectId, endpointId, githubOrg, githubRepo);
                await ado.CreateBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject, endpointId, repoId);
                _log.LogSuccess("Successfully configured Boards<->GitHub integration");
            }
            else
            {
                var repoId = await ado.GetBoardsGithubRepoId(adoOrg, adoOrgId, adoTeamProject, adoTeamProjectId, boardsConnection.endpointId, githubOrg, githubRepo);

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

                    await ado.AddRepoToBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject, boardsConnection.connectionId, boardsConnection.connectionName, boardsConnection.endpointId, repos);
                    _log.LogSuccess("Successfully configured Boards<->GitHub integration");
                }
            }
        }
    }
}