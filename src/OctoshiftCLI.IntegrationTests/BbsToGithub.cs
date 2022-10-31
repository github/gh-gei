using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

[Collection("Integration Tests")]
public sealed class BbsToGithub : IDisposable
{
    private const string BBS_URL = "http://e2e-bbs-8-5-0-linux-2204.eastus.cloudapp.azure.com:7990";
    private const string SSH_KEY_FILE = "ssh_key.pem";

    private readonly ITestOutputHelper _output;
    private readonly TestHelper _targetHelper;
    private readonly TestHelper _sourceHelper;
    private readonly HttpClient _versionClient;
    private readonly HttpClient _targetGithubHttpClient;
    private readonly GithubClient _targetGithubClient;
    private readonly GithubApi _targetGithubApi;
    private readonly HttpClient _sourceBbsHttpClient;
    private readonly BbsClient _sourceBbsClient;
    private readonly BbsApi _sourceBbsApi;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;

    public BbsToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        var logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceBbsUsername = Environment.GetEnvironmentVariable("BBS_USERNAME");
        var sourceBbsPassword = Environment.GetEnvironmentVariable("BBS_PASSWORD");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
        var azureStorageConnectionString = Environment.GetEnvironmentVariable($"AZURE_STORAGE_CONNECTION_STRING_{TestHelper.GetOsName().ToUpper()}");
        var sshKey = Environment.GetEnvironmentVariable("SSH_KEY");
        _tokens = new Dictionary<string, string>
        {
            ["BBS_USERNAME"] = sourceBbsUsername,
            ["BBS_PASSWORD"] = sourceBbsPassword,
            ["GH_PAT"] = targetGithubToken,
            ["AZURE_STORAGE_CONNECTION_STRING"] = azureStorageConnectionString
        };

        File.WriteAllText(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);

        _versionClient = new HttpClient();

        _sourceBbsHttpClient = new HttpClient();
        _sourceBbsClient = new BbsClient(logger, _sourceBbsHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), sourceBbsUsername, sourceBbsPassword);
        _sourceBbsApi = new BbsApi(_sourceBbsClient, BBS_URL, logger);

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(logger, _targetGithubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), new DateTimeProvider(), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(logger));

        _blobServiceClient = new BlobServiceClient(azureStorageConnectionString);

        _sourceHelper = new TestHelper(_output, _sourceBbsApi, _sourceBbsClient, BBS_URL);
        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Fact]
    public async Task Basic()
    {
        var githubTargetOrg = $"e2e-testing-{TestHelper.GetOsName()}";
        const string repo1 = "EEL-repo-1";
        const string repo2 = "EEL-repo-2";

        await _targetHelper.ResetBlobContainers();

        // TODO: Reset BBS test environment
        await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);

        // TODO: Generate BBS test data

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {BBS_URL} --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE}", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, repo1);
        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, repo2);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, repo1);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, repo2);

        // TODO: Assert migration logs are downloaded
    }

    public void Dispose()
    {
        _sourceBbsHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}
