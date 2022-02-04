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
        private readonly GithubClient _githubClient;
        private bool disposedValue;

        public GithubToGithub(ITestOutputHelper output)
        {
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            _githubHttpClient = new HttpClient();
            _githubClient = new GithubClient(logger, _githubHttpClient, githubToken);
            _githubApi = new GithubApi(_githubClient);

            _helper = new TestHelper(_output, _githubApi, _githubClient);
        }

        // Tracking Issue: https://github.com/github/octoshift/issues/3606
        [Fact(Skip = "random 404 errors")]
        public async Task Basic()
        {
            var githubSourceOrg = $"e2e-testing-source-{_helper.GetOsName()}";
            var githubTargetOrg = $"e2e-testing-{_helper.GetOsName()}";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2);

            await _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg}");

            await _helper.AssertGithubRepoExists(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoExists(githubTargetOrg, repo2);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
            await _helper.AssertGithubRepoInitialized(githubTargetOrg, repo2);
        }

        [Fact]
        public async Task BasicWithSsh()
        {
            var githubSourceOrg = $"e2e-testing-source-{_helper.GetOsName()}";
            var githubTargetOrg = $"e2e-testing-{_helper.GetOsName()}";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            await _helper.ResetGithubTestEnvironment(githubSourceOrg);
            await _helper.ResetGithubTestEnvironment(githubTargetOrg);

            await _helper.CreateGithubRepo(githubSourceOrg, repo1);
            await _helper.CreateGithubRepo(githubSourceOrg, repo2);

            await _helper.RunGeiCliMigration($"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --ssh");

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
