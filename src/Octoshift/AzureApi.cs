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
        private const string CONTAINER_PREFIX = "migration-archives";
        private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 24;
        public AzureApi(HttpClient client, BlobServiceClient blobServiceClient)
        {
            _client = client;
            _blobServiceClient = blobServiceClient;
        }

        public async Task<Stream> DownloadArchiveToStream(string fromUrl)
        {
            using var response = await _client.GetAsync(fromUrl);
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<Uri> UploadToBlob(string fileName, Stream stream)
        {
            var containerClient = await CreateBlobContainerAsync();
            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(stream, true);

            return GetServiceSasUriForBlob(blobClient);
        }

        private async Task<BlobContainerClient> CreateBlobContainerAsync()
        {
            var containerName = $"{CONTAINER_PREFIX}-{Guid.NewGuid()}";
            return await _blobServiceClient.CreateBlobContainerAsync(containerName);
        }

        private Uri GetServiceSasUriForBlob(BlobClient blobClient)
        {
            if (blobClient == null)
            {
                throw new ArgumentNullException(nameof(blobClient));
            }

            if (!blobClient.CanGenerateSasUri)
            {
                throw new InvalidOperationException("BlobClient object has not been authorized to generate shared key credentials. Verify --azure-storage-connection-key is valid and has proper permissions.");
            }

            var sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b"  // Resource = "b" for blobs, "c" for containers
            };

            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(AUTHORIZATION_TIMEOUT_IN_HOURS);
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

            return blobClient.GenerateSasUri(sasBuilder);
        }
    }
}
