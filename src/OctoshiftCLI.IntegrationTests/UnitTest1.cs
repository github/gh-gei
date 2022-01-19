using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.IntegrationTests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            using var httpClient = new HttpClient();
            var adoClient = new AdoClient(null, httpClient, adoToken);
            var adoApi = new AdoApi(adoClient);

            var adoOrg = "gei-e2e-testing";

            var teamProjects = await adoApi.GetTeamProjects(adoOrg);

            foreach (var teamProject in teamProjects)
            {
                if (teamProject != "service-connection-project-do-not-delete")
                {
                    var teamProjectId = await adoApi.GetTeamProjectId(adoOrg, teamProject);
                    await adoApi.DeleteTeamProject(adoOrg, teamProjectId);
                }
            }
        }
    }
}