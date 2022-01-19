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
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT"); ;
            using var httpClient = new HttpClient();
            var adoClient = new AdoClient(new OctoLogger(), httpClient, adoToken);
            var adoApi = new AdoApi(adoClient);

            var adoOrg = "gei-e2e-testing";

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
        }
    }
}