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
public sealed class BbsToGithub : IDisposable
{
    private const string SSH_KEY_FILE = "ssh_key.pem";
    private const string AWS_REGION = "us-east-1";
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

    public enum ArchiveUploadOption { AzureStorage, AwsS3, GithubStorage }

    public BbsToGithub(ITestOutputHelper output)
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
        var environmentVariableProvider = new EnvironmentVariableProvider(_logger);
        _archiveUploader = new ArchiveUploader(_targetGithubClient, UPLOADS_URL, _logger, retryPolicy, environmentVariableProvider);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger), _archiveUploader);

        _blobServiceClient = new BlobServiceClient(_azureStorageConnectionString);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Theory]
    [InlineData("https://e2e-bbs-linux-1.westus2.cloudapp.azure.com", true, ArchiveUploadOption.AzureStorage)]
    [InlineData("https://e2e-bbs-linux-1.westus2.cloudapp.azure.com", true, ArchiveUploadOption.AwsS3)]
    [InlineData("https://e2e-bbs-linux-1.westus2.cloudapp.azure.com", true, ArchiveUploadOption.GithubStorage)]
    public async Task Basic(string bbsServer, bool useSshForArchiveDownload, ArchiveUploadOption uploadOption)
    {
        var bbsProjectKey = $"E2E-{TestHelper.GetOsName().ToUpper()}";
        var githubTargetOrg = $"octoshift-e2e-bbs-{TestHelper.GetOsName()}";
        var repo1 = $"{bbsProjectKey}-repo-1";
        var targetRepo1 = $"{bbsProjectKey}-e2e-{TestHelper.GetOsName().ToLower()}-repo-1";

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
        });

        var sshPort = Environment.GetEnvironmentVariable("SSH_PORT_BBS");
        var archiveDownloadOptions = $" --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE} --ssh-port {sshPort}";
        if (useSshForArchiveDownload)
        {
            var sshKey = Environment.GetEnvironmentVariable("SSH_KEY_BBS");
            await File.WriteAllTextAsync(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);
        }
        else
        {
            archiveDownloadOptions = " --smb-user octoshift";
            _tokens.Add("SMB_PASSWORD", Environment.GetEnvironmentVariable("SMB_PASSWORD"));
        }

        var archiveUploadOptions = "";
        if (uploadOption == ArchiveUploadOption.AzureStorage)
        {
            _tokens.Add("AZURE_STORAGE_CONNECTION_STRING", _azureStorageConnectionString);
        }
        else if (uploadOption == ArchiveUploadOption.AwsS3)
        {
            _tokens.Add("AWS_ACCESS_KEY_ID", Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"));
            _tokens.Add("AWS_SECRET_ACCESS_KEY", Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"));
            var awsBucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME");
            archiveUploadOptions = $" --aws-bucket-name {awsBucketName} --aws-region {AWS_REGION}";
        }
        else if (uploadOption == ArchiveUploadOption.GithubStorage)
        {
            archiveUploadOptions = " --use-github-storage";
        }

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey}{archiveDownloadOptions}{archiveUploadOptions}", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo1);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo1);

        // TODO: Assert migration logs are downloaded
    }

    [Fact]
    public async Task MigrateRepo_MultipartUpload()
    {
        var githubTargetOrg = $"octoshift-e2e-bbs-{TestHelper.GetOsName()}";
        var bbsProjectKey = $"IN";
        var bbsServer = "https://e2e-bbs-linux-1.westus2.cloudapp.azure.com";
        var targetRepo = $"IN-100_cli";

        var sshPort = Environment.GetEnvironmentVariable("SSH_PORT_BBS");
        var sshKey = Environment.GetEnvironmentVariable("SSH_KEY_BBS");
        await File.WriteAllTextAsync(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);

        var retryPolicy = new RetryPolicy(null);
        await retryPolicy.Retry(async () =>
        {
            await _targetHelper.ResetGithubTestEnvironment(githubTargetOrg);
        });

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey} --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE} --ssh-port {sshPort} --use-github-storage", _tokens);

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
