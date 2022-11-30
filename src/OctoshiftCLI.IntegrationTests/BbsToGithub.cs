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

    private const string SSH_KEY_FILE = "ssh_key.pem";

    private readonly ITestOutputHelper _output;
    private readonly OctoLogger _logger;
    private readonly TestHelper _targetHelper;
    private readonly HttpClient _versionClient;
    private readonly HttpClient _targetGithubHttpClient;
    private readonly GithubClient _targetGithubClient;
    private readonly GithubApi _targetGithubApi;
    private readonly HttpClient _sourceBbsHttpClient;
    private readonly BbsClient _sourceBbsClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;

    public BbsToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        _logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

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
        _sourceBbsClient = new BbsClient(_logger, _sourceBbsHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), sourceBbsUsername, sourceBbsPassword);

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(_logger, _targetGithubHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), new DateTimeProvider(), targetGithubToken);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger));

        _blobServiceClient = new BlobServiceClient(azureStorageConnectionString);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Theory]
    [InlineData("http://e2e-bbs-8-5-0-linux-2204.eastus.cloudapp.azure.com:7990")]
    public async Task Basic(string bbsServer)
    {
        var bbsProjectKey = $"E2E-{TestHelper.GetOsName().ToUpper()}";
        var githubTargetOrg = $"e2e-testing-{TestHelper.GetOsName()}";
        var repo1 = $"{bbsProjectKey}-repo-1";
        var repo2 = $"{bbsProjectKey}-repo-2";

        var sourceBbsApi = new BbsApi(_sourceBbsClient, bbsServer, _logger);
        var sourceHelper = new TestHelper(_output, sourceBbsApi, _sourceBbsClient, bbsServer);

        var retryPolicy = new RetryPolicy(null);

        await retryPolicy.Retry(async () =>
        {
            await _targetHelper.ResetBlobContainers();
            await sourceHelper.ResetBbsTestEnvironment(bbsProjectKey);
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);

            await sourceHelper.CreateBbsProject(bbsProjectKey);
            await sourceHelper.CreateBbsRepo(bbsProjectKey, "repo-1");
            sourceHelper.InitializeBbsRepo(bbsProjectKey, "repo-1");
            await sourceHelper.CreateBbsRepo(bbsProjectKey, "repo-2");
            sourceHelper.InitializeBbsRepo(bbsProjectKey, "repo-2");
        });

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project-key {bbsProjectKey} --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE}", _tokens);

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
