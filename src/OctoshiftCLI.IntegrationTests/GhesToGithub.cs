using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

[Collection("Integration Tests")]
public sealed class GhesToGithub : IDisposable
{
    private const string GHES_API_URL = "https://octoshift-ghe.westus2.cloudapp.azure.com/api/v3";

    private readonly ITestOutputHelper _output;
    private readonly TestHelper _targetHelper;
    private readonly TestHelper _sourceHelper;
    private readonly HttpClient _versionClient;
    private readonly HttpClient _targetGithubHttpClient;
    private readonly GithubClient _targetGithubClient;
    private readonly GithubApi _targetGithubApi;
    private readonly HttpClient _sourceGithubHttpClient;
    private readonly GithubClient _sourceGithubClient;
    private readonly GithubApi _sourceGithubApi;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly Dictionary<string, string> _tokens;

    public GhesToGithub(ITestOutputHelper output)
    {
        _output = output;

        var logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceGithubToken = Environment.GetEnvironmentVariable("GHES_PAT");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
        var azureStorageConnectionString = Environment.GetEnvironmentVariable($"AZURE_STORAGE_CONNECTION_STRING_{TestHelper.GetOsName().ToUpper()}");
        _tokens = new Dictionary<string, string>
        {
            ["GH_SOURCE_PAT"] = sourceGithubToken,
            ["GH_PAT"] = targetGithubToken,
            ["AZURE_STORAGE_CONNECTION_STRING"] = azureStorageConnectionString
        };

        _versionClient = new HttpClient();

        _sourceGithubHttpClient = new HttpClient();
        _sourceGithubClient = new GithubClient(logger, _sourceGithubHttpClient, new VersionChecker(_versionClient, logger), sourceGithubToken);
        _sourceGithubApi = new GithubApi(_sourceGithubClient, GHES_API_URL, new RetryPolicy(logger));

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(logger, _targetGithubHttpClient, new VersionChecker(_versionClient, logger), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(logger));

        _blobServiceClient = new BlobServiceClient(azureStorageConnectionString);

        _sourceHelper = new TestHelper(_output, _sourceGithubApi, _sourceGithubClient) { GithubApiBaseUrl = GHES_API_URL };
        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Fact]
    public async Task Basic()
    {
        var githubSourceOrg = $"e2e-testing-{TestHelper.GetOsName()}";
        var githubTargetOrg = $"e2e-testing-{TestHelper.GetOsName()}";
        const string repo1 = "repo-1";
        const string repo2 = "repo-2";

        await _targetHelper.ResetBlobContainers();

        await _sourceHelper.ResetGithubTestEnvironment(githubSourceOrg);
        await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);

        await _sourceHelper.CreateGithubRepo(githubSourceOrg, repo1);
        await _sourceHelper.CreateGithubRepo(githubSourceOrg, repo2);

        await _targetHelper.RunGeiCliMigration(
            $"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --ghes-api-url {GHES_API_URL} --download-migration-logs", _tokens);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, repo1);
        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, repo2);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, repo2);

        _targetHelper.AssertMigrationLogFileExists(githubTargetOrg, repo1);
        _targetHelper.AssertMigrationLogFileExists(githubTargetOrg, repo2);
    }

    public void Dispose()
    {
        _sourceGithubHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}
