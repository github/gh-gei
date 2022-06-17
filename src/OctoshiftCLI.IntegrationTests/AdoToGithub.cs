using System;
using System.Collections.Generic;
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
        private readonly HttpClient _versionClient;
        private bool disposedValue;
        private readonly Dictionary<string, string> _tokens;

        public AdoToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });

            _versionClient = new HttpClient();
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            _adoHttpClient = new HttpClient();
            var retryPolicy = new RetryPolicy(logger);
            var adoClient = new AdoClient(logger, _adoHttpClient, new VersionChecker(_versionClient, logger), retryPolicy, adoToken);
            var adoApi = new AdoApi(adoClient, "https://dev.azure.com", logger);

            var githubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
            _githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, _githubHttpClient, new VersionChecker(_versionClient, logger), githubToken);
            var githubApi = new GithubApi(githubClient, "https://api.github.com", new RetryPolicy(logger));

            _tokens = new Dictionary<string, string>
            {
                ["GH_PAT"] = githubToken,
                ["ADO_PAT"] = adoToken
            };

            _helper = new TestHelper(_output, adoApi, githubApi, adoClient, githubClient);
        }

        [Fact]
        public async Task Basic()
        {
            var adoOrg = $"gei-e2e-testing-{TestHelper.GetOsName()}";
            var githubOrg = $"e2e-testing-{TestHelper.GetOsName()}";
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

            await _helper.RunAdoToGithubCliMigration($"generate-script --github-org {githubOrg} --ado-org {adoOrg} --all", _tokens);

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _adoHttpClient.Dispose();
                    _githubHttpClient.Dispose();
                    _versionClient.Dispose();
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
