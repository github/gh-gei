using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace OctoshiftCLI
{
    public class AzureApi
    {
        private readonly HttpClient _client;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _connectionString;

        private const int Timeout_In_Hours = 1;
        public AzureApi(HttpClient client, BlobServiceClient blobServiceClient, string connectionString)
        {
            _client = client;
            _blobServiceClient = blobServiceClient;
            _connectionString = connectionString;
        }

        public async Task DownloadFileTo(string fromUrl, string toFilePath)
        {
            using var response = await _client.GetAsync(fromUrl, HttpCompletionOption.ResponseHeadersRead);
            using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
            using Stream streamToWriteTo = File.Open(toFilePath, FileMode.Create);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
        }

        public async Task<Uri> UploadToBlob(string fileName, string filePath)
        {
            var containerClient = await CreateBlobContainerAsync();
            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(filePath, true);

            return GetServiceSasUriForBlob(blobClient);
        }

        public async Task<BlobContainerClient> CreateBlobContainerAsync()
        {
            var containerName = "migration-archives-" + Guid.NewGuid();
            return await _blobServiceClient.CreateBlobContainerAsync(containerName);
        }

        public Uri GetServiceSasUriForBlob(BlobClient blobClient, string storedPolicyName = null)
        {
            if (blobClient == null)
            {
                throw new ArgumentNullException(nameof(blobClient));
            }

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
                    sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(Timeout_In_Hours);
                    sasBuilder.SetPermissions(BlobSasPermissions.Read |
                        BlobSasPermissions.Write);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                // _log.LogInformation($"Generated SAS URI for {blobClient.Name} at: {sasUri}");

                return sasUri;
            }
            else
            {
                // _log.LogInformation("Error generating SAS URI: BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                return null;
            }
        }
    }
}
