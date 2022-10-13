using System;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace OctoshiftCLI
{
    public class AzureApi
    {
        private readonly HttpClient _client;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly OctoLogger _log;
        private const string CONTAINER_PREFIX = "migration-archives";
        private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 24;

        public AzureApi(HttpClient client, BlobServiceClient blobServiceClient, OctoLogger log)
        {
            _client = client;
            _blobServiceClient = blobServiceClient;
            _log = log;

            if (_client is not null)
            {
                _client.Timeout = new TimeSpan(1, 0, 0);
            }
        }

        public virtual async Task<byte[]> DownloadArchive(string fromUrl)
        {
            _log.LogVerbose($"HTTP GET: {fromUrl}");
            using var response = await _client.GetAsync(fromUrl);
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        public virtual async Task<Uri> UploadToBlob(string fileName, byte[] content)
        {
            var containerClient = await CreateBlobContainerAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            var options = new BlobUploadOptions
            {
                TransferOptions = new Azure.Storage.StorageTransferOptions()
                {
                    InitialTransferSize = 4 * 1024 * 1024,
                    MaximumTransferSize = 4 * 1024 * 1024
                },
            };

            var binaryDataContent = new BinaryData(content);
            await blobClient.UploadAsync(binaryDataContent, options);
            return GetServiceSasUriForBlob(blobClient);
        }

        private async Task<BlobContainerClient> CreateBlobContainerAsync()
        {
            var containerName = $"{CONTAINER_PREFIX}-{Guid.NewGuid()}";
            return await _blobServiceClient.CreateBlobContainerAsync(containerName);
        }

        private Uri GetServiceSasUriForBlob(BlobClient blobClient)
        {
            if (!blobClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("BlobClient object has not been authorized to generate shared key credentials. Verify --azure-storage-connection-key is valid and has proper permissions.");
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b",  // Resource = "b" for blobs, "c" for containers

                ExpiresOn = DateTimeOffset.UtcNow.AddHours(AUTHORIZATION_TIMEOUT_IN_HOURS)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

            return blobClient.GenerateSasUri(sasBuilder);
        }
    }
}
