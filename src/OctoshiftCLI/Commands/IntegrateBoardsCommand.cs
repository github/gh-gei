using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class IntegrateBoardsCommand : Command
    {
        private AdoApi _ado;

        public IntegrateBoardsCommand() : base("integrate-boards")
        {
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

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(githubOrg);
            AddOption(githubRepos);

            Handler = CommandHandler.Create<string, string, string, string>(Invoke);
        }

        private async Task Invoke(string adoOrg, string adoTeamProject, string githubOrg, string githubRepos)
        {
            Console.WriteLine("Integrating Azure Boards...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPOS: {githubRepos}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _ado = new AdoApi(adoToken);

            var githubRepoList = ParseRepoList(githubRepos);

            var userId = await _ado.GetUserId();
            var adoOrgId = await _ado.GetOrganizationId(userId, adoOrg);
            var adoTeamProjectId = await _ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var githubHandle = await _ado.GetGithubHandle(adoOrg, adoOrgId, adoTeamProject, githubToken);
            var endpointId = await _ado.CreateEndpoint(adoOrg, adoTeamProjectId, githubToken, githubHandle);

            var repoIds = await _ado.GetGithubRepoIds(adoOrg, adoOrgId, adoTeamProject, adoTeamProjectId, endpointId, githubOrg, githubRepoList);

            await _ado.CreateBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject, endpointId, repoIds);
        }

        private IEnumerable<string> ParseRepoList(string githubRepos) => githubRepos.Split(",").Select(x => x.Trim());
    }
}
