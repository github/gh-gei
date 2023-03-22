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
                await _helper.ResetAdoTestEnvironment(adoOrg);
                await _helper.ResetGithubTestEnvironment(githubOrg);

                await _helper.CreateTeamProject(adoOrg, teamProject1);
                var commitId = await _helper.InitializeAdoRepo(adoOrg, teamProject1, adoRepo1);
                await _helper.CreatePipeline(adoOrg, teamProject1, adoRepo1, pipeline1, commitId);

                await _helper.CreateTeamProject(adoOrg, teamProject2);
                commitId = await _helper.InitializeAdoRepo(adoOrg, teamProject2, adoRepo2);
                await _helper.CreatePipeline(adoOrg, teamProject2, adoRepo2, pipeline2, commitId);
            });

            await _helper.RunCliCommand($"ado2gh inventory-report --ado-org {adoOrg}", "gh", _tokens);
            await _helper.RunAdoToGithubCliMigration($"generate-script --github-org {githubOrg} --ado-org {adoOrg} --all --repo-list repos.csv", _tokens);

            _helper.AssertNoErrorInLogs(_startTime);

            await _helper.AssertGithubRepoExists(githubOrg, $"{teamProject1}-{teamProject1}");
            await _helper.AssertGithubRepoExists(githubOrg, $"{teamProject2}-{teamProject2}");
            await _helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject1}-{teamProject1}");
            await _helper.AssertGithubRepoInitialized(githubOrg, $"{teamProject2}-{teamProject2}");
            await _helper.AssertAutolinkConfigured(githubOrg, $"{teamProject1}-{teamProject1}", $"https://dev.azure.com/{adoOrg}/{teamProject1}/_workitems/edit/<num>/");
            await _helper.AssertAutolinkConfigured(githubOrg, $"{teamProject2}-{teamProject2}", $"https://dev.azure.com/{adoOrg}/{teamProject2}/_workitems/edit/<num>/");
            await _helper.AssertAdoRepoDisabled(adoOrg, teamProject1, adoRepo1);
            await _helper.AssertAdoRepoDisabled(adoOrg, teamProject2, adoRepo2);
            await _helper.AssertAdoRepoLocked(adoOrg, teamProject1, adoRepo1);
            await _helper.AssertAdoRepoLocked(adoOrg, teamProject2, adoRepo2);
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-Maintainers");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-Admins");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-Maintainers");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-Admins");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-Maintainers", $"{teamProject1}-Maintainers");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-Admins", $"{teamProject1}-Admins");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-Maintainers", $"{teamProject2}-Maintainers");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-Admins", $"{teamProject2}-Admins");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-Maintainers", $"{teamProject1}-{teamProject1}", "maintain");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-Admins", $"{teamProject1}-{teamProject1}", "admin");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-Maintainers", $"{teamProject2}-{teamProject2}", "maintain");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-Admins", $"{teamProject2}-{teamProject2}", "admin");
            await _helper.AssertServiceConnectionWasShared(adoOrg, teamProject1);
            await _helper.AssertServiceConnectionWasShared(adoOrg, teamProject2);
            await _helper.AssertPipelineRewired(adoOrg, teamProject1, pipeline1, githubOrg, $"{teamProject1}-{teamProject1}");
            await _helper.AssertPipelineRewired(adoOrg, teamProject2, pipeline2, githubOrg, $"{teamProject2}-{teamProject2}");
            await _helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject1);
            await _helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject2);
            _helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject1}-{teamProject1}");
            _helper.AssertMigrationLogFileExists(githubOrg, $"{teamProject2}-{teamProject2}");
        }
    }
}
