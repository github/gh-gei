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
    public class GenerateArchiveAzBlobCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GenerateArchiveAzBlobCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("generate-archive")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub Migration API's to generate a migration archive";
            Description += Environment.NewLine;
            Description += "Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used instead. This authenticates to source GHES API";

            var githubURL = new Option<string>("--github-url")
            {
                IsRequired = false
            };
            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true
            };
            var githubSourceRepo = new Option<string>("--github-source-repo")
            {
                IsRequired = true
            };
            var azureStorageConnectionString = new Option<string>("--azure-storage-connection-string")
            {
                IsRequired = true
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubURL);
            AddOption(githubSourceOrg);
            AddOption(githubSourceRepo);
            AddOption(azureStorageConnectionString);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string githubURL, string githubSourceOrg, string githubSourceRepo, string azureStorageConnectionString, bool verbose = false)
        {
            _log.Verbose = verbose;

            // TODO: Insert generate archive code here

            // TODO: Update these with the real url locations
            var gitArchiveLocation = "https://raw.githubusercontent.com/github/gh-gei/main/images/CodeLayers.png";
            var metadataArchiveLocation = "https://raw.githubusercontent.com/github/gh-gei/main/images/CodeLayers.png";

            // TODO: Update these with the real file names
            var gitArchiveFileName = "gitArchive.png";
            var metadataArchiveFileName = "metadataArchive.png";

            var gitArchiveFilePath = "/tmp/" + gitArchiveFileName;
            var metadataArchiveFilePath = "/tmp/" + metadataArchiveFileName;

            // Download both archives to the local filesystem
            await DownloadFileTo(gitArchiveLocation, gitArchiveFilePath);
            await DownloadFileTo(metadataArchiveLocation, metadataArchiveFilePath);

            var blobServiceClient = new BlobServiceClient(azureStorageConnectionString);
            var containerName = "migration-archives-" + Guid.NewGuid().ToString();
            BlobContainerClient containerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);

            _log.LogInformation($"Created blob container {containerName} in storage account: {blobServiceClient.AccountName}");
            _ = await UploadFileToBlob(containerClient, gitArchiveFileName, gitArchiveFilePath);
            _ = await UploadFileToBlob(containerClient, metadataArchiveFileName, metadataArchiveFilePath);

            // TODO: Kick off Octoshift migration, pass in both SAS URLs
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