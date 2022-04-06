using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    [Collection("Integration Tests")]
    public class AdoToGithub : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestHelper _helper;

        private readonly HttpClient _adoHttpClient;
        private readonly HttpClient _githubHttpClient;
        private bool disposedValue;

        public AdoToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            _adoHttpClient = new HttpClient();
            var adoClient = new AdoClient(logger, _adoHttpClient, new VersionChecker(), adoToken);
            var adoApi = new AdoApi(adoClient);

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            _githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, _githubHttpClient, new VersionChecker(), githubToken);
            var githubApi = new GithubApi(githubClient, "https://api.github.com", new RetryPolicy(logger));

            _helper = new TestHelper(_output, adoApi, githubApi, adoClient, githubClient);
        }

        [Fact]
        public async Task Basic()
        {
            var adoOrg = $"gei-e2e-testing-{_helper.GetOsName()}";
            var githubOrg = $"e2e-testing-{_helper.GetOsName()}";
            var teamProject1 = "gei-e2e-1";
            var teamProject2 = "gei-e2e-2";
            var adoRepo1 = teamProject1;
            var adoRepo2 = teamProject2;
            var pipeline1 = "pipeline1";
            var pipeline2 = "pipeline2";

            await _helper.ResetAdoTestEnvironment(adoOrg);
            await _helper.ResetGithubTestEnvironment(githubOrg);

            await _helper.CreateTeamProject(adoOrg, teamProject1);
            var commitId = await _helper.InitializeAdoRepo(adoOrg, teamProject1, adoRepo1);
            await _helper.CreatePipeline(adoOrg, teamProject1, adoRepo1, pipeline1, commitId);

            await _helper.CreateTeamProject(adoOrg, teamProject2);
            commitId = await _helper.InitializeAdoRepo(adoOrg, teamProject2, adoRepo2);
            await _helper.CreatePipeline(adoOrg, teamProject2, adoRepo2, pipeline2, commitId);

            await _helper.RunAdoToGithubCliMigration($"generate-script --github-org {githubOrg} --ado-org {adoOrg}");

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
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-maintainers");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject1}-admins");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-maintainers");
            await _helper.AssertGithubTeamCreated(githubOrg, $"{teamProject2}-admins");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-maintainers", $"{teamProject1}-maintainers");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject1}-admins", $"{teamProject1}-admins");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-maintainers", $"{teamProject2}-maintainers");
            await _helper.AssertGithubTeamIdpLinked(githubOrg, $"{teamProject2}-admins", $"{teamProject2}-admins");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-maintainers", $"{teamProject1}-{teamProject1}", "maintain");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject1}-admins", $"{teamProject1}-{teamProject1}", "admin");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-maintainers", $"{teamProject2}-{teamProject2}", "maintain");
            await _helper.AssertGithubTeamHasRepoRole(githubOrg, $"{teamProject2}-admins", $"{teamProject2}-{teamProject2}", "admin");
            await _helper.AssertServiceConnectionWasShared(adoOrg, teamProject1);
            await _helper.AssertServiceConnectionWasShared(adoOrg, teamProject2);
            await _helper.AssertPipelineRewired(adoOrg, teamProject1, pipeline1, githubOrg, $"{teamProject1}-{teamProject1}");
            await _helper.AssertPipelineRewired(adoOrg, teamProject2, pipeline2, githubOrg, $"{teamProject2}-{teamProject2}");
            await _helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject1);
            await _helper.AssertBoardsIntegrationConfigured(adoOrg, teamProject2);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _adoHttpClient.Dispose();
                    _githubHttpClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
