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
        private const string Container_Prefix = "migration-archives";
        private const int Authorization_Timeout_In_Hours = 24;
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
            var containerName = $"{Container_Prefix}-{Guid.NewGuid()}";
            return await _blobServiceClient.CreateBlobContainerAsync(containerName);
        }

        public Uri GetServiceSasUriForBlob(BlobClient blobClient)
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

            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(Authorization_Timeout_In_Hours);
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

            return blobClient.GenerateSasUri(sasBuilder);
        }
    }
}
