using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using OctoshiftCLI.Services;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

[Collection("Integration Tests")]
public sealed class GhesToGithub : IDisposable
{
    private const string GHES_API_URL = "https://octoshift-ghe.westus2.cloudapp.azure.com/api/v3";
    private const string UPLOADS_URL = "https://uploads.github.com";

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
    private readonly DateTime _startTime;
    private readonly ArchiveUploader _archiveUploader;
    private readonly string _azureStorageConnectionString;

    public GhesToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        var logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceGithubToken = Environment.GetEnvironmentVariable("GHES_PAT");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
        _azureStorageConnectionString = Environment.GetEnvironmentVariable($"AZURE_STORAGE_CONNECTION_STRING_GHES_{TestHelper.GetOsName().ToUpper()}");
        _tokens = new Dictionary<string, string>
        {
            ["GH_SOURCE_PAT"] = sourceGithubToken,
            ["GH_PAT"] = targetGithubToken,
        };

        _versionClient = new HttpClient();
        var retryPolicy = new RetryPolicy(logger);
        var environmentVariableProvider = new EnvironmentVariableProvider(logger);

        _sourceGithubHttpClient = new HttpClient();
        _sourceGithubClient = new GithubClient(logger, _sourceGithubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), new DateTimeProvider(), sourceGithubToken);
        _archiveUploader = new ArchiveUploader(_targetGithubClient, UPLOADS_URL, logger, retryPolicy, environmentVariableProvider);
        _sourceGithubApi = new GithubApi(_sourceGithubClient, GHES_API_URL, new RetryPolicy(logger), _archiveUploader);

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(logger, _targetGithubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), new DateTimeProvider(), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(logger), _archiveUploader);

        _blobServiceClient = new BlobServiceClient(_azureStorageConnectionString);

        _sourceHelper = new TestHelper(_output, _sourceGithubApi, _sourceGithubClient) { GithubApiBaseUrl = GHES_API_URL };
        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Basic(bool useGithubStorage)
    {
        var githubSourceOrg = $"e2e-testing-{TestHelper.GetOsName()}";
        var githubTargetOrg = $"octoshift-e2e-ghes-{TestHelper.GetOsName()}";
        const string repo1 = "repo-1";
        const string repo2 = "repo-2";

        var retryPolicy = new RetryPolicy(null);

        if (!useGithubStorage)
        {
            _tokens["AZURE_STORAGE_CONNECTION_STRING"] = _azureStorageConnectionString;
        }

        await retryPolicy.Retry(async () =>
        {
            if (!useGithubStorage)
            {
                await _targetHelper.ResetBlobContainers();
            }

            await _sourceHelper.ResetGithubTestEnvironment(githubSourceOrg);
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);

            await _sourceHelper.CreateGithubRepo(githubSourceOrg, repo1);
            await _sourceHelper.CreateGithubRepo(githubSourceOrg, repo2);
        });

        // Build the command with conditional option
        var command = $"generate-script --github-source-org {githubSourceOrg} --github-target-org {githubTargetOrg} --ghes-api-url {GHES_API_URL} --download-migration-logs";
        if (useGithubStorage)
        {
            command += " --use-github-storage";
        }

        await _targetHelper.RunGeiCliMigration(command, _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

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
