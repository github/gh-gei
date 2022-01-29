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
        private bool disposedValue;

        public GithubToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            _githubSourceHttpClient = new HttpClient();
            var githubSourceClient = new GithubClient(logger, _githubSourceHttpClient, githubToken);
            _githubSourceApi = new GithubApi(githubSourceClient);

            _githubTargetHttpClient = new HttpClient();
            var githubTargetClient = new GithubClient(logger, _githubTargetHttpClient, githubToken);
            _githubTargetApi = new GithubApi(githubTargetClient);

            _helper = new TestHelper(_output, _githubSourceApi, _githubTargetApi);
        }

        // Tracking Issue: https://github.com/github/octoshift/issues/3606
        [Fact(Skip = "random 404 errors")]
        public async Task Basic()
        {
            var githubSourceOrg = "e2e-testing-source";
            var githubTargetOrg = "e2e-testing";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2);

            _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg}");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2);
        }

        // Tracking Issue: https://github.com/github/octoshift/issues/3525
        [Fact(Skip = "bug where it says permanently added XXXX to list of known hosts")]
        public async Task BasicWithSsh()
        {
            var githubSourceOrg = "e2e-testing-source";
            var githubTargetOrg = "e2e-testing";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2);

            _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --ssh");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2);
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