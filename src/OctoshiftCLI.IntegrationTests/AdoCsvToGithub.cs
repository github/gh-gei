using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    [Collection("Integration Tests")]
    public class AdoCsvToGithub : AdoToGithub
    {
        public AdoCsvToGithub(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task With_Inventory_Report_Csv()
        {
            var adoOrg = $"gei-e2e-testing-csv-{TestHelper.GetOsName()}";
            var githubOrg = $"e2e-testing-ado-csv-{TestHelper.GetOsName()}";
            var teamProject1 = "gei-e2e-1";
            var teamProject2 = "gei-e2e-2";
            var adoRepo1 = teamProject1;
            var adoRepo2 = teamProject2;
            var pipeline1 = "pipeline1";
            var pipeline2 = "pipeline2";

            var retryPolicy = new RetryPolicy(null);

            await retryPolicy.Retry(async () =>
            {
                await Helper.ResetAdoTestEnvironment(adoOrg);
                await Helper.ResetGithubTestEnvironment(githubOrg);

                await Helper.CreateTeamProject(adoOrg, teamProject1);
                var commitId = await Helper.InitializeAdoRepo(adoOrg, teamProject1, adoRepo1);
                await Helper.CreatePipeline(adoOrg, teamProject1, adoRepo1, pipeline1, commitId);

                await Helper.CreateTeamProject(adoOrg, teamProject2);
                commitId = await Helper.InitializeAdoRepo(adoOrg, teamProject2, adoRepo2);
                await Helper.CreatePipeline(adoOrg, teamProject2, adoRepo2, pipeline2, commitId);
            });

            await Helper.RunCliCommand($"ado2gh inventory-report --ado-org {adoOrg}", "gh", Tokens);
            await Helper.RunAdoToGithubCliMigration($"generate-script --github-org {githubOrg} --ado-org {adoOrg} --all --repo-list repos.csv", Tokens);

            Helper.AssertNoErrorInLogs(StartTime);

            await Helper.AssertGithubRepoExists(githubOrg, $"{teamProject1}-{teamProject1}");
            await Helper.AssertGithubRepoExists(githubOrg, $"{teamProject2}-{teamProject2}");
            await Helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject1}-{teamProject1}");
            await Helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject2}-{teamProject2}");
            await Helper.AssertAutolinkConfigured(githubOrg, $"{teamProject1}-{teamProject1}", $"https://dev.azure.com/{adoOrg}/{teamProject1}/_workitems/edit/<num>/");
            await Helper.AssertAutolinkConfigured(githubOrg, $"{teamProject2}-{teamProject2}", $"https://dev.azure.com/{adoOrg}/{teamProject2}/_workitems/edit/<num>/");
            await Helper.AssertAdoRepoDisabled(adoOrg, teamProject1, adoRepo1);
            await Helper.AssertAdoRepoDisabled(adoOrg, teamProject2, adoRepo2);
            await Helper.AssertAdoRepoLocked(adoOrg, teamProject1, adoRepo1);
            await Helper.AssertAdoRepoLocked(adoOrg, teamProject2, adoRepo2);
            await Helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-Maintainers");
            await Helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-Admins");
            await Helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-Maintainers");
            await Helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-Admins");
            await Helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-Maintainers", $"{teamProject1}-Maintainers");
            await Helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-Admins", $"{teamProject1}-Admins");
            await Helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-Maintainers", $"{teamProject2}-Maintainers");
            await Helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-Admins", $"{teamProject2}-Admins");
            await Helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-Maintainers", $"{teamProject1}-{teamProject1}", "maintain");
            await Helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-Admins", $"{teamProject1}-{teamProject1}", "admin");
            await Helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-Maintainers", $"{teamProject2}-{teamProject2}", "maintain");
            await Helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-Admins", $"{teamProject2}-{teamProject2}", "admin");
            await Helper.AssertServiceConnectionWasShared(adoOrg, teamProject1);
            await Helper.AssertServiceConnectionWasShared(adoOrg, teamProject2);
            await Helper.AssertPipelineRewired(adoOrg, teamProject1, pipeline1, githubOrg, $"{teamProject1}-{teamProject1}");
            await Helper.AssertPipelineRewired(adoOrg, teamProject2, pipeline2, githubOrg, $"{teamProject2}-{teamProject2}");
            await Helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject1);
            await Helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject2);
            Helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject1}-{teamProject1}");
            Helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject2}-{teamProject2}");
        }
    }
}
