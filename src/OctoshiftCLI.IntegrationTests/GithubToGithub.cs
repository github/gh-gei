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
        private readonly GithubApi _githubSourceApi;
        private readonly GithubApi _githubTargetApi;
        private readonly TestHelper _helper;

        private readonly HttpClient _githubSourceHttpClient;
        private readonly HttpClient _githubTargetHttpClient;
        private readonly GithubClient _githubSourceClient;
        private readonly GithubClient _githubTargetClient;
        private bool disposedValue;

        public GithubToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            _githubSourceHttpClient = new HttpClient();
            _githubSourceClient = new GithubClient(logger, _githubSourceHttpClient, githubToken);
            _githubSourceApi = new GithubApi(_githubSourceClient);

            _githubTargetHttpClient = new HttpClient();
            _githubTargetClient = new GithubClient(logger, _githubTargetHttpClient, githubToken);
            _githubTargetApi = new GithubApi(_githubTargetClient);

            _helper = new TestHelper(_output, _githubSourceApi, _githubTargetApi);
        }

        // Tracking Issue: https://github.com/github/octoshift/issues/3606
        [Fact(Skip = "random 404 errors")]
        public async Task Basic()
        {
            var githubSourceOrg = $"e2e-testing-source-{_helper.GetOsName()}";
            var githubTargetOrg = $"e2e-testing-{_helper.GetOsName()}";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg, _githubSourceClient);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg, _githubTargetClient);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1, _githubSourceClient);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2, _githubSourceClient);

            _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg}");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1, _githubTargetClient);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2, _githubTargetClient);
        }

        [Fact]
        public async Task BasicWithSsh()
        {
            var githubSourceOrg = $"e2e-testing-source-{_helper.GetOsName()}";
            var githubTargetOrg = $"e2e-testing-{_helper.GetOsName()}";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg, _githubSourceClient);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg, _githubTargetClient);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1, _githubSourceClient);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2, _githubSourceClient);

            _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --ssh");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1, _githubTargetClient);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2, _githubTargetClient);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _githubSourceHttpClient.Dispose();
                    _githubTargetHttpClient.Dispose();
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