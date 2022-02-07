﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateArchiveRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly IAzureApiFactory _azureApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        private const int Archive_Generataion_Timeout_In_Hours = 10;
        private const int Check_Status_Delay_In_Milliseconds = 10000; // 10 seconds
        private const string Git_Archive_File_Name = "gitArchive.tar.gz";
        private const string Metadata_Archive_File_Name = "metadataArchive.tar.gz";

        public MigrateArchiveRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider, IAzureApiFactory azureApiFactory) : base("migrate-archive-repo")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _azureApiFactory = azureApiFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _targetGithubApiFactory = targetGithubApiFactory;

            Description = "Generates migration archives, uploads them to Azure Blob Storage, then invokes the GitHub Migration APIs to migrate the repo and all repo data using those uploaded archives.";
            Description += Environment.NewLine;
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API.";

            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = true,
                Description = "The api endpoint for the hostname of your GHES instance. For example: https://api.myghes.com"
            };
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };
            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = false,
                Description = "Defaults to the name of source-repo"
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = true,
                Description = "The connection string for the Azure storage account. For example: DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(ghesApiUrl);
            AddOption(githubSourceOrg);
            AddOption(sourceRepo);
            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string ghesApiUrl, string githubSourceOrg, string sourceRepo, string githubTargetOrg, string targetRepo, string azureStorageConnectionString, bool noSslVerify = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            // Log all the parameters, except for the connection string, which is a secret
            _log.LogInformation("Starting Migration Archives...");
            _log.LogInformation($"GHES SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GHES SOURCE REPO: {sourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"GHES API URL: {ghesApiUrl}");

            var dateTimeNow = DateTime.Now;

            if (string.IsNullOrWhiteSpace(targetRepo))
            {
                _log.LogInformation($"Target repo name not provided, defaulting to same as source repo ({sourceRepo}) + timestamp");
                targetRepo = $"{sourceRepo}-{dateTimeNow:MMdd-HHmm}";
            }

            _log.LogInformation($"TARGET REPO: {targetRepo}");

            if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
            {
                _log.LogInformation("--azure-storage-connection-string not set, using environment variable AZURE_STORAGE_CONNECTION_STRING");
                azureStorageConnectionString = _environmentVariableProvider.AzureStorageConnectionString();

                if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
                {
                    throw new OctoshiftCliException("Please set either --azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING");
                }
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var githubApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSSL(ghesApiUrl) : _sourceGithubApiFactory.Create(ghesApiUrl);
            var azureApi = noSslVerify ? _azureApiFactory.CreateClientNoSSL(azureStorageConnectionString) : _azureApiFactory.Create(azureStorageConnectionString);

            var gitDataArchiveId = await githubApi.StartGitArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await githubApi.StartMetadataArchiveGeneration(githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var metadataArchiveUrl = await WaitForArchiveGeneration(githubApi, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive (metadata) download url: {metadataArchiveUrl}");
            var gitArchiveUrl = await WaitForArchiveGeneration(githubApi, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive (git) download url: {gitArchiveUrl}");

            var timeNow = $"{dateTimeNow:yyyy-MM-dd_HH-mm-ss}";

            var gitArchiveFileName = $"{timeNow}-{Git_Archive_File_Name}";
            var metadataArchiveFileName = $"{timeNow}-{Metadata_Archive_File_Name}";

            var gitArchiveFilePath = $"/tmp/{gitArchiveFileName}";
            var metadataArchiveFilePath = $"/tmp/{metadataArchiveFileName}";

            // Download both archives to the local filesystem
            _log.LogInformation($"Downloading archive from {gitArchiveUrl} to {gitArchiveFilePath}");
            await azureApi.DownloadFileTo(gitArchiveUrl, gitArchiveFilePath);
            _log.LogInformation($"Downloading archive from {metadataArchiveUrl} to {metadataArchiveFilePath}");
            await azureApi.DownloadFileTo(metadataArchiveUrl, metadataArchiveFilePath);

            _log.LogInformation($"Uploading archive {gitArchiveFileName} to Azure Blob Storage");
            var authenticatedGitArchiveUrl = await azureApi.UploadToBlob(gitArchiveFileName, gitArchiveFilePath);
            _log.LogInformation($"Uploading archive {metadataArchiveFileName} to Azure Blob Storage");
            var authenticatedMetadataArchiveUrl = await azureApi.UploadToBlob(metadataArchiveFileName, metadataArchiveFilePath);

            _log.LogInformation($"Deleting local archive files");
            File.Delete(gitArchiveFilePath);
            File.Delete(metadataArchiveFilePath);

            // Run migrate repo command
            var migrateRepoCommand = new MigrateRepoCommand(_log, _targetGithubApiFactory, _environmentVariableProvider);
            await migrateRepoCommand.Invoke(
                githubSourceOrg,
                "",
                "",
                sourceRepo,
                githubTargetOrg,
                targetRepo,
                "",
                authenticatedMetadataArchiveUrl.ToString(),
                authenticatedGitArchiveUrl.ToString(),
                false,
                verbose
            );
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string githubSourceOrg, int archiveId)
        {
            var timeout = DateTime.Now.AddHours(Archive_Generataion_Timeout_In_Hours);
            while (DateTime.Now < timeout)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(githubSourceOrg, archiveId);
                }
                if (archiveStatus == ArchiveMigrationStatus.Failed)
                {
                    throw new OctoshiftCliException($"Archive generation failed for id: {archiveId}");
                }
                await Task.Delay(Check_Status_Delay_In_Milliseconds);
            }
            throw new TimeoutException($"Archive generation timed out after {Archive_Generataion_Timeout_In_Hours} hours");
        }
    }
}
