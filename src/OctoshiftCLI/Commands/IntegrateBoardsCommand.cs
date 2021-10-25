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

        public async Task Invoke(string adoOrg, string adoTeamProject, string githubOrg, string githubRepos)
        {
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            using var ado = AdoApiFactory.Create(adoToken);

            await IntegrateBoards(adoOrg, adoTeamProject, githubOrg, githubRepos, ado, githubToken);
        }

        private async Task IntegrateBoards(string adoOrg, string adoTeamProject, string githubOrg, string githubRepos, AdoApi ado, string githubToken)
        {
            Console.WriteLine("Integrating Azure Boards...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPOS: {githubRepos}");

            var githubRepoList = ParseRepoList(githubRepos);

            var userId = await ado.GetUserId();
            var adoOrgId = await ado.GetOrganizationId(userId, adoOrg);
            var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var githubHandle = await ado.GetGithubHandle(adoOrg, adoOrgId, adoTeamProject, githubToken);
            var endpointId = await ado.CreateEndpoint(adoOrg, adoTeamProjectId, githubToken, githubHandle);

            var repoIds = await ado.GetGithubRepoIds(adoOrg, adoOrgId, adoTeamProject, adoTeamProjectId, endpointId, githubOrg, githubRepoList);

            await ado.CreateBoardsGithubConnection(adoOrg, adoOrgId, adoTeamProject, endpointId, repoIds);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully configured Boards<->GitHub integration");
            Console.ResetColor();
        }

        private IEnumerable<string> ParseRepoList(string githubRepos) => githubRepos.Split(",").Select(x => x.Trim());
    }
}
