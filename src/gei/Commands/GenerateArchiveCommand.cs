using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;


namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateArchiveCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        private const int Timeout_In_Hours = 10;
        private const int Delay_In_Milliseconds = 10000; // 10 seconds

        public GenerateArchiveCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to the source GHES API.";

            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
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
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = true
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
            AddOption(azureStorageConnectionString);
            AddOption(noSslVerify);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool, bool>(Invoke);
        }

        public async Task Invoke(string ghesApiUrl, string githubSourceOrg, string sourceRepo, string azureStorageConnectionString, bool noSslVerify = false, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating Migration Archives...");
            _log.LogInformation($"GHES SOURCE ORG: {githubSourceOrg}");
            _log.LogInformation($"GHES SOURCE REPO: {sourceRepo}");

            if (string.IsNullOrWhiteSpace(ghesApiUrl))
            {
                _log.LogInformation("--ghes-api-url not provided, defaulting to https://api.github.com");
                ghesApiUrl = "https://api.github.com";
            }
            else
            {
                _log.LogInformation($"GHES API URL: {ghesApiUrl}");
            }

            if (noSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }

            var githubApi = noSslVerify ? _sourceGithubApiFactory.CreateClientNoSSL() : _sourceGithubApiFactory.Create();

            var gitDataArchiveId = await githubApi.StartGitArchiveGeneration(ghesApiUrl, githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of git data started with id: {gitDataArchiveId}");
            var metadataArchiveId = await githubApi.StartMetadataArchiveGeneration(ghesApiUrl, githubSourceOrg, sourceRepo);
            _log.LogInformation($"Archive generation of metadata started with id: {metadataArchiveId}");

            var metadataArchiveUrl = await WaitForArchiveGeneration(githubApi, ghesApiUrl, githubSourceOrg, metadataArchiveId);
            _log.LogInformation($"Archive(metadata) download url: {metadataArchiveUrl}");
            var gitArchiveUrl = await WaitForArchiveGeneration(githubApi, ghesApiUrl, githubSourceOrg, gitDataArchiveId);
            _log.LogInformation($"Archive(git) download url: {gitArchiveUrl}");

            var timeNow = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            // TODO: Update these with the real file names
            var gitArchiveFileName = $"{timeNow}gitArchive.tar.gz";
            var metadataArchiveFileName = $"{timeNow}metadataArchive.tar.gz";

            var gitArchiveFilePath = "/tmp/" + gitArchiveFileName;
            var metadataArchiveFilePath = "/tmp/" + metadataArchiveFileName;

            // Download both archives to the local filesystem
            await DownloadFileTo(gitArchiveUrl, gitArchiveFilePath);
            await DownloadFileTo(metadataArchiveUrl, metadataArchiveFilePath);

            var blobServiceClient = new BlobServiceClient(azureStorageConnectionString);
            var containerName = "migration-archives-" + Guid.NewGuid();
            BlobContainerClient containerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);

            _log.LogInformation($"Created blob container {containerName} in storage account: {blobServiceClient.AccountName}");
            _ = await UploadFileToBlob(containerClient, gitArchiveFileName, gitArchiveFilePath);
            _ = await UploadFileToBlob(containerClient, metadataArchiveFileName, metadataArchiveFilePath);
        }

        private async Task<string> WaitForArchiveGeneration(GithubApi githubApi, string ghesApiUrl, string githubSourceOrg, int archiveId)
        {
            var timeOut = DateTime.Now.AddHours(Timeout_In_Hours);
            while (DateTime.Now < timeOut)
            {
                var archiveStatus = await githubApi.GetArchiveMigrationStatus(ghesApiUrl, githubSourceOrg, archiveId);
                _log.LogInformation($"Waiting for archive with id {archiveId} generation to finish. Current status: {archiveStatus}");
                if (archiveStatus == ArchiveMigrationStatus.Exported)
                {
                    return await githubApi.GetArchiveMigrationUrl(ghesApiUrl, githubSourceOrg, archiveId);
                }
                if (archiveStatus == ArchiveMigrationStatus.Failed)
                {
                    throw new OctoshiftCliException($"Archive generation failed with id: {archiveId}");
                }
                await Task.Delay(Delay_In_Milliseconds);
            }
            throw new TimeoutException($"Archive generation timed out after {Timeout_In_Hours} hours");
        }

        private async Task DownloadFileTo(string fromUrl, string toFilePath)
        {
            using var client = new HttpClient();
            _log.LogInformation($"Downloading file from {fromUrl}...");

            using (var response = await client.GetAsync(fromUrl, HttpCompletionOption.ResponseHeadersRead))
            using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
            {
                using Stream streamToWriteTo = File.Open(toFilePath, FileMode.Create);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);
            }

            _log.LogInformation($"Download completed. Wrote output to: {toFilePath}");
        }

        private async Task<Uri> UploadFileToBlob(BlobContainerClient containerClient, string fileName, string filePath)
        {
            var blobClient = containerClient.GetBlobClient(fileName);

            _log.LogInformation($"Uploading {filePath} to blob container {containerClient.Name}...");

            await blobClient.UploadAsync(filePath, true);

            _log.LogInformation($"Upload completed for {filePath}");

            return GetServiceSasUriForBlob(blobClient);
        }

        private Uri GetServiceSasUriForBlob(BlobClient blobClient, string storedPolicyName = null)
        {
            var sasDurationInHours = 1;
            _log.LogInformation($"Generating SAS URI with {sasDurationInHours} hour duration for {blobClient.Name}...");

            // Check whether this BlobClient object has been authorized with Shared Key.
            if (blobClient.CanGenerateSasUri)
            {
                var sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                    BlobName = blobClient.Name,
                    Resource = "b"
                };

                if (storedPolicyName == null)
                {
                    sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(sasDurationInHours);
                    sasBuilder.SetPermissions(BlobSasPermissions.Read |
                        BlobSasPermissions.Write);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                _log.LogInformation($"Generated SAS URI for {blobClient.Name} at: {sasUri}");

                return sasUri;
            }
            else
            {
                _log.LogInformation("Error generating SAS URI: BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                return null;
            }
        }
    }
}