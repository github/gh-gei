using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using OctoshiftCLI.Services;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests;

[Collection("Integration Tests")]
public sealed class BbsAzureLinuxToGithub : IDisposable
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
    private readonly ArchiveUploader _archiveUploader;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;
    private readonly string _azureStorageConnectionString;

    public BbsAzureLinuxToGithub(ITestOutputHelper output)
    {
        _startTime = DateTime.Now;
        _output = output;

        _logger = new OctoLogger(_ => { }, x => _output.WriteLine(x), _ => { }, _ => { });

        var sourceBbsUsername = Environment.GetEnvironmentVariable("BBS_USERNAME");
        var sourceBbsPassword = Environment.GetEnvironmentVariable("BBS_PASSWORD");
        var targetGithubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
        _azureStorageConnectionString = Environment.GetEnvironmentVariable($"AZURE_STORAGE_CONNECTION_STRING_BBS_{TestHelper.GetOsName().ToUpper()}");
        _tokens = new Dictionary<string, string>
        {
            ["GHEC_PAT"] = targetGithubToken,
            ["BBS_USERNAME"] = sourceBbsUsername,
            ["BBS_PASSWORD"] = sourceBbsPassword
        };

        _targetHelper = new TestHelper(_output, targetGithubToken);

        _versionClient = new HttpClient();
        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(_logger, _targetGithubHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), new DateTimeProvider(), targetGithubToken);
        var retryPolicy = new RetryPolicy(_logger);
        var environmentVariableProvider = new EnvironmentVariableProvider(_logger);
        _archiveUploader = new ArchiveUploader(_targetGithubClient, _logger, retryPolicy, environmentVariableProvider);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger), _archiveUploader);

        _blobServiceClient = new BlobServiceClient(_azureStorageConnectionString);

        _sourceBbsHttpClient = new HttpClient();
        _sourceBbsClient = new BbsClient(_logger, _sourceBbsHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), new DateTimeProvider(), sourceBbsUsername, sourceBbsPassword);
    }

    [Fact]
    public async Task MigrateRepo_BbsLinux_AzureStorage()
    {
        var bbsServer = "http://e2e-bbs-8-5-0-linux-2204.westus2.cloudapp.azure.com:7990";
        var bbsProjectKey = $"E2E-{TestHelper.GetOsName().ToUpper()}";
        var githubTargetOrg = $"octoshift-e2e-bbs-{TestHelper.GetOsName()}";
        var repo1 = $"{bbsProjectKey}-repo-1";
        var repo2 = $"{bbsProjectKey}-repo-2";
        var targetRepo1 = $"{bbsProjectKey}-e2e-{TestHelper.GetOsName().ToLower()}-repo-1";
        var targetRepo2 = $"{bbsProjectKey}-e2e-{TestHelper.GetOsName().ToLower()}-repo-2";

        var sourceBbsApi = new BbsApi(_sourceBbsClient, bbsServer, _logger);
        var sourceHelper = new TestHelper(_output, sourceBbsApi, _sourceBbsClient, bbsServer);

        var retryPolicy = new RetryPolicy(null);

        await retryPolicy.Retry(async () =>
        {
            await _targetHelper.ResetBlobContainers();
            await sourceHelper.ResetBbsTestEnvironment(bbsProjectKey);
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);

            await sourceHelper.CreateBbsProject(bbsProjectKey);
            await sourceHelper.CreateBbsRepo(bbsProjectKey, repo1);
            await sourceHelper.InitializeBbsRepo(bbsProjectKey, repo1);
            await sourceHelper.CreateBbsRepo(bbsProjectKey, repo2);
            await sourceHelper.InitializeBbsRepo(bbsProjectKey, repo2);
        });

        // Use SSH for archive download
        var sshKey = Environment.GetEnvironmentVariable("SSH_KEY_BBS_8_5_0");
        await File.WriteAllTextAsync(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);
        var archiveDownloadOptions = $" --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE}";

        // Use Azure Storage
        _tokens.Add("AZURE_STORAGE_CONNECTION_STRING", _azureStorageConnectionString);

        await _targetHelper.RunBbsCliMigration(
            $"migrate-repo --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey} --bbs-repo {repo1} --github-repo {targetRepo1}{archiveDownloadOptions}", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo1);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo1);

        await _targetHelper.RunBbsCliMigration(
            $"migrate-repo --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey} --bbs-repo {repo2} --github-repo {targetRepo2}{archiveDownloadOptions}", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo2);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo2);
    }

    public void Dispose()
    {
        _sourceBbsHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}