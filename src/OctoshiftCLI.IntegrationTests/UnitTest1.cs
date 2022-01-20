using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    public class UnitTest1
    {
        private readonly ITestOutputHelper output;

        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task Test1()
        {
            var logger = new OctoLogger();

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT"); ;
            using var adoHttpClient = new HttpClient();
            var adoClient = new AdoClient(logger, adoHttpClient, adoToken);
            var adoApi = new AdoApi(adoClient);

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            using var githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, githubHttpClient, githubToken);
            var githubApi = new GithubApi(githubClient);

            var adoOrg = "gei-e2e-testing";
            var githubOrg = "e2e-testing";

            var teamProjects = await adoApi.GetTeamProjects(adoOrg);

            output.WriteLine($"Found {teamProjects.Count()} Team Projects");

            foreach (var teamProject in teamProjects)
            {
                if (teamProject != "service-connection-project-do-not-delete")
                {
                    output.WriteLine($"Deleting Team Project: {adoOrg}\\{teamProject}...");
                    var teamProjectId = await adoApi.GetTeamProjectId(adoOrg, teamProject);
                    await adoApi.DeleteTeamProject(adoOrg, teamProjectId);
                }
            }

            var githubRepos = await githubApi.GetRepos(githubOrg);

            foreach (var repo in githubRepos)
            {
                output.WriteLine($"Deleting GitHub repo: {repo}...");
                await githubApi.DeleteRepo(githubOrg, repo);
            }

            var githubTeams = await githubApi.GetTeams(githubOrg);

            foreach (var team in githubTeams)
            {
                output.WriteLine($"Deleting GitHub team: {team}");
                await githubApi.DeleteTeam(githubOrg, team);
            }
        }
    }
}