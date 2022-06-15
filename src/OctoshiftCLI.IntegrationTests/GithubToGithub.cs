using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    [Collection("Integration Tests")]
    public class GithubToGithub : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly GithubApi _githubApi;
        private readonly TestHelper _helper;

        private readonly HttpClient _githubHttpClient;
        private readonly HttpClient _versionClient;
        private readonly GithubClient _githubClient;
        private bool disposedValue;

        public GithubToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            _githubHttpClient = new HttpClient();
            _versionClient = new HttpClient();
            _githubClient = new GithubClient(logger, _githubHttpClient, new VersionChecker(_versionClient, logger), githubToken);
            _githubApi = new GithubApi(_githubClient, "https://api.github.com", new RetryPolicy(logger));

            _helper = new TestHelper(_output, _githubApi, _githubClient);
        }

        [Fact]
        public async Task Basic()
        {
            var githubSourceOrg = $"e2e-testing-source-{TestHelper.GetOsName()}";
            var githubTargetOrg = $"e2e-testing-{TestHelper.GetOsName()}";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2);

            await _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --download-migration-logs");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2);

            _helper.AssertMigrationLogFileExists(githubTargetOrg, repo1);
            _helper.AssertMigrationLogFileExists(githubTargetOrg, repo2);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
