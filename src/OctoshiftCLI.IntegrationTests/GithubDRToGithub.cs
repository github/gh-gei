using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Services;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

// Integration test for GitHub Enterprise Cloud with data residency (GithubDR) source migrations.
// Requires E2E_SOURCE_PROXIMA_PAT and GHEC_PAT secrets to be set. The source repo is treated as
// read-only (tenant-managed); only the target org is reset each run.
[Collection("Integration Tests")]
public sealed class GithubDRToGithub : IDisposable
{
    private const string GITHUBDR_API_URL = "https://api.migration-tools-staffwus201.ghe.com";
    private const string GITHUBDR_SOURCE_ORG = "octoshift";
    private const string GITHUBDR_SOURCE_REPO = "tiny";
    private const string UPLOADS_URL = "https://uploads.github.com";

    private readonly ITestOutputHelper _output;
    private readonly TestHelper _targetHelper;
    private readonly HttpClient _versionClient;
    private readonly HttpClient _targetGithubHttpClient;
    private readonly GithubClient _targetGithubClient;
    private readonly GithubApi _targetGithubApi;
    private readonly HttpClient _sourceGithubHttpClient;
    private readonly GithubClient _sourceGithubClient;
    private readonly GithubApi _sourceGithubApi;
    private readonly ArchiveUploader _archiveUploader;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;

    public GithubDRToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        TestHelper.AssertCredentialsPresent(
            ("E2E_SOURCE_PROXIMA_PAT", "GitHub Enterprise Cloud with data residency (source) personal access token"),
            ("GHEC_PAT", "GitHub Enterprise Cloud (target) personal access token"));

        var logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceGithubToken = Environment.GetEnvironmentVariable("E2E_SOURCE_PROXIMA_PAT");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");

        _tokens = new Dictionary<string, string>
        {
            ["GH_SOURCE_PAT"] = sourceGithubToken,
            ["GH_PAT"] = targetGithubToken,
        };

        _versionClient = new HttpClient();
        var retryPolicy = new RetryPolicy(logger, "GithubDR (E2E_SOURCE_PROXIMA_PAT)");
        var environmentVariableProvider = new EnvironmentVariableProvider(logger);

        _sourceGithubHttpClient = new HttpClient();
        _sourceGithubClient = new GithubClient(logger, _sourceGithubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger, "GithubDR (E2E_SOURCE_PROXIMA_PAT)"), new DateTimeProvider(), sourceGithubToken);
        _archiveUploader = new ArchiveUploader(_targetGithubClient, UPLOADS_URL, logger, retryPolicy, environmentVariableProvider);
        _sourceGithubApi = new GithubApi(_sourceGithubClient, GITHUBDR_API_URL, new RetryPolicy(logger, "GithubDR (E2E_SOURCE_PROXIMA_PAT)"), _archiveUploader);

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(logger, _targetGithubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger, "GitHub (GHEC_PAT)"), new DateTimeProvider(), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(logger, "GitHub (GHEC_PAT)"), _archiveUploader);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient);
    }

    [Fact]
    public async Task Basic()
    {
        var githubTargetOrg = $"octoshift-e2e-githubdr-{TestHelper.GetOsName()}";

        var retryPolicy = new RetryPolicy(null);

        // Source repo is tenant-managed (read-only); only reset the target.
        await retryPolicy.Retry(async () => await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg));

        var command = $"gei migrate-repo --github-source-org {GITHUBDR_SOURCE_ORG} --source-repo {GITHUBDR_SOURCE_REPO} --github-source-api-url {GITHUBDR_API_URL} --github-target-org {githubTargetOrg} --target-repo {GITHUBDR_SOURCE_REPO} --target-repo-visibility private --use-github-storage";

        await _targetHelper.RunCliCommand(command, "gh", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, GITHUBDR_SOURCE_REPO);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, GITHUBDR_SOURCE_REPO);
    }

    public void Dispose()
    {
        _sourceGithubHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}
