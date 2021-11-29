using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class IntegrateBoardsCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoFactory;
        private readonly GithubApiFactory _githubFactory;

        public IntegrateBoardsCommand(OctoLogger log, AdoApiFactory adoFactory, GithubApiFactory githubFactory) : base("integrate-boards")
        {
            _log = log;
            _adoFactory = adoFactory;
            _githubFactory = githubFactory;

            Description = "Configures the Azure Boards<->GitHub integration in Azure DevOps.";

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
            var githubRepos = new Option<string>("--github-repos")
            {
                IsRequired = true,
                Description = "Comma separated list of github repo names"
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubOrg);
            AddOption(githubRepos);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string githubOrg, string githubRepos, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Integrating Azure Boards...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"GITHUB REPOS: {githubRepos}");

            var githubRepoList = ParseRepoList(githubRepos);

            using var ado = _adoFactory.Create();
            var githubToken = _githubFactory.GetGithubToken();

            var userId = await ado.GetUserId();
            var adoOrgId = await ado.GetOrganizationId(userId, adoOrg);
            var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var githubHandle = await ado.GetGithubHandle(adoOrg, adoOrgId, adoTeamProject, githubToken);
            var endpointId = await ado.CreateEndpoint(adoOrg, adoTeamProjectId, githubToken, githubHandle);

            var repoIds = await ado.GetGithubRepoIds(adoOrg, adoOrgId, adoTeamProject, adoTeamProjectId, endpointId, githubOrg, githubRepoList);

            await ado.CreateBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject, endpointId, repoIds);

            _log.LogSuccess("Successfully configured Boards<->GitHub integration");
        }

        private IEnumerable<string> ParseRepoList(string githubRepos) => githubRepos?.Split(",").Select(x => x.Trim());
    }
}