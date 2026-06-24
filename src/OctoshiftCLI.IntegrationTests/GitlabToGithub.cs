using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Services;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

[Collection("Integration Tests")]
public sealed class GitlabToGithub : IDisposable
{
    private const string GitlabServerUrl = "https://gitlab.com";
    private const string GitlabGroup = "Mouse-Hack";
    private const string GitlabProject = "no-merge-requests";

    private readonly ITestOutputHelper _output;
    private readonly OctoLogger _logger;
    private readonly TestHelper _targetHelper;
    private readonly HttpClient _versionClient;
    private readonly HttpClient _targetGithubHttpClient;
    private readonly GithubClient _targetGithubClient;
    private readonly GithubApi _targetGithubApi;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;

    public GitlabToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        TestHelper.AssertCredentialsPresent(
            ("GITLAB_PAT", "GitLab.com personal access token"),
            ("GHEC_PAT", "GitHub Enterprise Cloud personal access token"));

        _logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceGitlabToken = Environment.GetEnvironmentVariable("GITLAB_PAT");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");

        _tokens = new Dictionary<string, string>
        {
            ["GITLAB_PAT"] = sourceGitlabToken,
            ["GH_PAT"] = targetGithubToken
        };

        _versionClient = new HttpClient();

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(_logger, _targetGithubHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger, "GitHub (GHEC_PAT)"), new DateTimeProvider(), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger, "GitHub (GHEC_PAT)"), null);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient);
    }

    [Fact]
    public async Task Basic()
    {
        var githubTargetOrg = $"octoshift-e2e-gitlab-{TestHelper.GetOsName()}";
        // generate-script derives the GitHub repo name from "{group}-{project}".
        var targetRepo = $"{GitlabGroup}-{GitlabProject}";

        // Pre-clean: wipe the target org so a prior run doesn't influence this one.
        // Matches the pattern used by every other adapter's integration test.
        var retryPolicy = new RetryPolicy(null);
        await retryPolicy.Retry(async () =>
        {
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);
        });

        // Exercise inventory-report against the source group.
        await _targetHelper.RunCliCommand(
            $"gl2gh inventory-report --gitlab-server-url {GitlabServerUrl} --gitlab-group {GitlabGroup}",
            "gh",
            _tokens);

        // Exercise generate-script + run the generated migrate.ps1, scoped to a single project
        // so we only ever migrate the repo we own for this test.
        await _targetHelper.RunCliMigration(
            $"gl2gh generate-script --gitlab-server-url {GitlabServerUrl} --gitlab-group {GitlabGroup} --gitlab-project {GitlabProject} --github-org {githubTargetOrg} --use-github-storage",
            "gh",
            _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo);
    }

    public void Dispose()
    {
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}
