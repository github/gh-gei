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
public sealed class BbsGithubMultipartToGithub : IDisposable
{
    private const string SSH_KEY_FILE = "ssh_key.pem";
    private const string UPLOADS_URL = "https://uploads.github.com";

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

    public BbsGithubMultipartToGithub(ITestOutputHelper output)
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
            ["BBS_USERNAME"] = sourceBbsUsername,
            ["BBS_PASSWORD"] = sourceBbsPassword,
            ["GH_PAT"] = targetGithubToken
        };

        _versionClient = new HttpClient();

        _sourceBbsHttpClient = new HttpClient();
        _sourceBbsClient = new BbsClient(_logger, _sourceBbsHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), sourceBbsUsername, sourceBbsPassword);

        _targetGithubHttpClient = new HttpClient();
        _targetGithubClient = new GithubClient(_logger, _targetGithubHttpClient, new VersionChecker(_versionClient, _logger), new RetryPolicy(_logger), new DateTimeProvider(), targetGithubToken);
        var retryPolicy = new RetryPolicy(_logger);
        _archiveUploader = new ArchiveUploader(_targetGithubClient, UPLOADS_URL, _logger, retryPolicy);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger), _archiveUploader);

        _blobServiceClient = new BlobServiceClient(_azureStorageConnectionString);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Fact]
    public async Task MigrateRepo_MultipartUpload()
    {
        var githubTargetOrg = $"octoshift-e2e-bbs-{TestHelper.GetOsName()}";
        var bbsProjectKey = $"IN";
        var bbsServer = "http://e2e-bbs-8-5-0-linux-2204.westus2.cloudapp.azure.com:7990";
        var targetRepo = $"IN-100_cli";

        var sshKey = Environment.GetEnvironmentVariable("SSH_KEY_BBS_8_5_0");
        await File.WriteAllTextAsync(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);

        var retryPolicy = new RetryPolicy(null);
        await retryPolicy.Retry(async () =>
        {
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);
        });

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey} --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE} --use-github-storage", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo);
    }

    public void Dispose()
    {
        _sourceBbsHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}