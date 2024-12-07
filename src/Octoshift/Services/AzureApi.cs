using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class AzureApi
{
    private readonly HttpClient _client;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly OctoLogger _log;
    private readonly object _mutex = new();
    private const string CONTAINER_PREFIX = "migration-archives";
    private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 48;
    private const int DEFAULT_BLOCK_SIZE = 4 * 1024 * 1024;
    private const int UPLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;
    private DateTime _nextProgressReport = DateTime.Now;

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
        using var memoryStream = new MemoryStream(content);
        return await UploadToBlob(fileName, memoryStream);
    }

    public virtual async Task<Uri> UploadToBlob(string fileName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(fileName, nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);

        var containerClient = await CreateBlobContainerAsync();
        var blobClient = containerClient.GetBlobClient(fileName);

        var progress = new Progress<long>();
        var archiveSize = content.Length;
        progress.ProgressChanged += (_, uploadedBytes) => LogProgress(uploadedBytes, archiveSize);

        var options = new BlobUploadOptions
        {
            TransferOptions = new Azure.Storage.StorageTransferOptions()
            {
                InitialTransferSize = DEFAULT_BLOCK_SIZE,
                MaximumTransferSize = DEFAULT_BLOCK_SIZE
            },
            ProgressHandler = progress
        };

        await blobClient.UploadAsync(content, options);
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
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder);
    }

    private void LogProgress(long uploadedBytes, long totalBytes)
    {
        lock (_mutex)
        {
            if (DateTime.Now < _nextProgressReport)
            {
                return;
            }

            _nextProgressReport = _nextProgressReport.AddSeconds(UPLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS);
        }

        var percentage = (int)(uploadedBytes * 100L / totalBytes);
        var progressMessage = uploadedBytes > 0
            ? $", {uploadedBytes.ToLogFriendlySize()} out of {totalBytes.ToLogFriendlySize()} ({percentage}%) completed"
            : "";

        _log.LogInformation($"Archive upload in progress{progressMessage}...");
    }
}
