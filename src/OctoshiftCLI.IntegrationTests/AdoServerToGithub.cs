using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    [Collection("Integration Tests")]
    public class AdoServerToGithub : AdoToGithub
    {
        private const string ADO_SERVER_URL = "http://octoshift-ado-server-2022.eastus.cloudapp.azure.com/";

        public AdoServerToGithub(ITestOutputHelper output) : base(output, ADO_SERVER_URL, "ADO_SERVER_PAT")
        {
        }

        [Fact]
        public async Task Basic()
        {
            var adoOrg = $"gei-e2e-testing-basic-{TestHelper.GetOsName()}";
            var githubOrg = $"e2e-testing-ado-server-{TestHelper.GetOsName()}";
            var teamProject1 = "gei-e2e-1";
            var teamProject2 = "gei-e2e-2";
            var adoRepo1 = teamProject1;
            var adoRepo2 = teamProject2;
            var pipeline1 = "pipeline1";
            var pipeline2 = "pipeline2";

            var retryPolicy = new RetryPolicy(null);

            await retryPolicy.Retry(async () =>
            {
                await Helper.ResetAdoTestEnvironment(adoOrg, ADO_SERVER_URL);
                await Helper.ResetGithubTestEnvironment(githubOrg);

                await Helper.CreateTeamProject(adoOrg, teamProject1, ADO_SERVER_URL);
                var commitId = await Helper.InitializeAdoRepo(adoOrg, teamProject1, adoRepo1, ADO_SERVER_URL);
                await Helper.CreatePipeline(adoOrg, teamProject1, adoRepo1, pipeline1, commitId, ADO_SERVER_URL);

                await Helper.CreateTeamProject(adoOrg, teamProject2, ADO_SERVER_URL);
                commitId = await Helper.InitializeAdoRepo(adoOrg, teamProject2, adoRepo2, ADO_SERVER_URL);
                await Helper.CreatePipeline(adoOrg, teamProject2, adoRepo2, pipeline2, commitId, ADO_SERVER_URL);
            });

            await Helper.RunAdoToGithubCliMigration($"generate-script --github-org {githubOrg} --ado-org {adoOrg} --ado-server-url {ADO_SERVER_URL}", Tokens);

            Helper.AssertNoErrorInLogs(StartTime);

            await Helper.AssertGithubRepoExists(githubOrg, $"{teamProject1}-{teamProject1}");
            await Helper.AssertGithubRepoExists(githubOrg, $"{teamProject2}-{teamProject2}");
            await Helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject1}-{teamProject1}");
            await Helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject2}-{teamProject2}");
            Helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject1}-{teamProject1}");
            Helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject2}-{teamProject2}");
        }
    }
}
