using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
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
    private readonly ArchiveUploader _multipartUploader;
    private readonly Dictionary<string, string> _tokens;
    private readonly DateTime _startTime;
    private readonly string _azureStorageConnectionString;

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
        _multipartUploader = new ArchiveUploader(_targetGithubClient, _logger);
        _targetGithubApi = new GithubApi(_targetGithubClient, "https://api.github.com", new RetryPolicy(_logger), _multipartUploader);

        _blobServiceClient = new BlobServiceClient(_azureStorageConnectionString);

        _targetHelper = new TestHelper(_output, _targetGithubApi, _targetGithubClient, _blobServiceClient);
    }

    [Theory]
    [InlineData("http://e2e-bbs-8-5-0-linux-2204.eastus.cloudapp.azure.com:7990", true, true)]
    [InlineData("http://e2e-bbs-7-21-9-win-2019.eastus.cloudapp.azure.com:7990", false, true)]
    [InlineData("http://e2e-bbs-8-5-0-linux-2204.eastus.cloudapp.azure.com:7990", true, false)]
    public async Task Basic(string bbsServer, bool useSshForArchiveDownload, bool useAzureForArchiveUpload)
    {
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

        var archiveDownloadOptions = $" --ssh-user octoshift --ssh-private-key {SSH_KEY_FILE}";
        if (useSshForArchiveDownload)
        {
            var sshKey = Environment.GetEnvironmentVariable(GetSshKeyName(bbsServer));
            await File.WriteAllTextAsync(Path.Join(TestHelper.GetOsDistPath(), SSH_KEY_FILE), sshKey);
        }
        else
        {
            archiveDownloadOptions = " --smb-user octoshift";
            _tokens.Add("SMB_PASSWORD", Environment.GetEnvironmentVariable("SMB_PASSWORD"));
        }

        var archiveUploadOptions = "";
        if (useAzureForArchiveUpload)
        {
            _tokens.Add("AZURE_STORAGE_CONNECTION_STRING", _azureStorageConnectionString);
        }
        else
        {
            _tokens.Add("AWS_ACCESS_KEY_ID", Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"));
            _tokens.Add("AWS_SECRET_ACCESS_KEY", Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"));
            var awsBucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME");
            archiveUploadOptions = $" --aws-bucket-name {awsBucketName} --aws-region {AWS_REGION}";
        }

        await _targetHelper.RunBbsCliMigration(
            $"generate-script --github-org {githubTargetOrg} --bbs-server-url {bbsServer} --bbs-project {bbsProjectKey}{archiveDownloadOptions}{archiveUploadOptions}", _tokens);

        _targetHelper.AssertNoErrorInLogs(_startTime);

        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo1);
        await _targetHelper.AssertGithubRepoExists(githubTargetOrg, targetRepo2);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo1);
        await _targetHelper.AssertGithubRepoInitialized(githubTargetOrg, targetRepo2);

        // TODO: Assert migration logs are downloaded
    }

    private string GetSshKeyName(string bbsServer)
    {
        var bbsVersion = Regex.Match(bbsServer, @"e2e-bbs-(\d{1,2}-\d{1,2}-\d{1,2})").Groups[1].Value.Replace('-', '_');
        return $"SSH_KEY_BBS_{bbsVersion}";
    }

    public void Dispose()
    {
        _sourceBbsHttpClient?.Dispose();
        _targetGithubHttpClient?.Dispose();
        _versionClient?.Dispose();
    }
}
