using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class ArchiveUploader
{
    private const int BYTES_PER_MEBIBYTE = 1024 * 1024;
    private const int MIN_MULTIPART_MEBIBYTES = 5; // 5 MiB minimum size for multipart upload. Don't allow overrides smaller than this.
    private const int DEFAULT_MULTIPART_MEBIBYTES = 100;

    private readonly GithubClient _client;
    private readonly string _uploadsUrl;
    private readonly OctoLogger _log;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    internal int _streamSizeLimit = DEFAULT_MULTIPART_MEBIBYTES * BYTES_PER_MEBIBYTE; // 100 MiB stored in bytes
    private readonly RetryPolicy _retryPolicy;

    public ArchiveUploader(GithubClient client, string uploadsUrl, OctoLogger log, RetryPolicy retryPolicy, EnvironmentVariableProvider environmentVariableProvider)
    {
        _client = client;
        _uploadsUrl = uploadsUrl;
        _log = log;
        _retryPolicy = retryPolicy;
        _environmentVariableProvider = environmentVariableProvider;

        SetStreamSizeLimitFromEnvironment();
    }
    public virtual async Task<string> Upload(Stream archiveContent, string archiveName, string orgDatabaseId)
    {
        if (archiveContent == null)
        {
            throw new ArgumentNullException(nameof(archiveContent), "The archive content stream cannot be null.");
        }

        using var streamContent = new StreamContent(archiveContent);
        streamContent.Headers.ContentType = new("application/octet-stream");

        var isMultipart = archiveContent.Length > _streamSizeLimit; // Determines if stream size is greater than 100MB

        string response;

        if (isMultipart)
        {
            var url = $"{_uploadsUrl}/organizations/{orgDatabaseId.EscapeDataString()}/gei/archive/blobs/uploads";

            response = await UploadMultipart(archiveContent, archiveName, url);
            return response;
        }
        else
        {
            var url = $"{_uploadsUrl}/organizations/{orgDatabaseId.EscapeDataString()}/gei/archive?name={archiveName.EscapeDataString()}";

            response = await _retryPolicy.Retry(async () => await _client.PostAsync(url, streamContent));
            var data = JObject.Parse(response);
            return (string)data["uri"];
        }
    }

    private async Task<string> UploadMultipart(Stream archiveContent, string archiveName, string uploadUrl)
    {
        var buffer = new byte[_streamSizeLimit];

        try
        {
            // 1. Start the upload
            var startHeaders = await StartUpload(uploadUrl, archiveName, archiveContent.Length);
            var nextUrl = GetNextUrl(startHeaders);

            // 2. Upload parts
            int bytesRead;
            var partsRead = 0;
            var totalParts = (long)Math.Ceiling((double)archiveContent.Length / _streamSizeLimit);
            while ((bytesRead = await archiveContent.ReadAsync(buffer)) > 0)
            {
                nextUrl = await UploadPart(buffer, bytesRead, nextUrl.ToString(), partsRead, totalParts);
                partsRead++;
            }

            // 3. Complete the upload
            var geiUri = await CompleteUpload(nextUrl.ToString());

            return geiUri.ToString();
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed during multipart upload.", ex);
        }
    }

    private async Task<IEnumerable<KeyValuePair<string, IEnumerable<string>>>> StartUpload(string uploadUrl, string archiveName, long contentSize)
    {
        _log.LogInformation($"Starting archive upload into GitHub owned storage: {archiveName}...");

        var body = new
        {
            content_type = "application/octet-stream",
            name = archiveName,
            size = contentSize
        };

        try
        {
            var (_, headers) = await _retryPolicy.Retry(async () => await _client.PostWithFullResponseAsync(uploadUrl, body));
            return headers.ToList();
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to start upload.", ex);
        }
    }

    private async Task<Uri> UploadPart(byte[] body, int bytesRead, string nextUrl, int partsRead, long totalParts)
    {
        _log.LogInformation($"Uploading part {partsRead + 1}/{totalParts}...");
        using var content = new ByteArrayContent(body, 0, bytesRead);
        content.Headers.ContentType = new("application/octet-stream");

        try
        {
            // Make the PATCH request and retrieve headers
            var (_, headers) = await _retryPolicy.Retry(async () => await _client.PatchWithFullResponseAsync(nextUrl, content));

            // Retrieve the next URL from the response headers
            return GetNextUrl(headers.ToList());
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to upload part.", ex);
        }
    }

    private async Task<Uri> CompleteUpload(string lastUrl)
    {
        try
        {
            var response = await _retryPolicy.Retry(async () => await _client.PutAsync(lastUrl, ""));
            var responseData = JObject.Parse(response);

            _log.LogInformation("Finished uploading archive");

            return new Uri((string)responseData["uri"]);
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to complete upload.", ex);
        }
    }

    private Uri GetNextUrl(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        // Use FirstOrDefault to safely handle missing Location headers
        var locationHeader = headers.First(header => header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(locationHeader.Key))
        {
            var locationValue = locationHeader.Value.FirstOrDefault();
            if (locationValue.HasValue())
            {
                return new Uri(new Uri(_uploadsUrl), locationValue);
            }
        }
        throw new OctoshiftCliException("Location header is missing in the response, unable to retrieve next URL for multipart upload.");
    }

    private void SetStreamSizeLimitFromEnvironment()
    {
        var envValue = _environmentVariableProvider.GithubOwnedStorageMultipartMebibytes();
        if (!int.TryParse(envValue, out var limitInMebibytes) || limitInMebibytes <= 0)
        {
            return;
        }

        if (limitInMebibytes < MIN_MULTIPART_MEBIBYTES)
        {
            _log.LogWarning($"GITHUB_OWNED_STORAGE_MULTIPART_MEBIBYTES is set to {limitInMebibytes} MiB, but the minimum value is {MIN_MULTIPART_MEBIBYTES} MiB. Using default value of {DEFAULT_MULTIPART_MEBIBYTES} MiB.");
            return;
        }

        var limitBytes = (int)((long)limitInMebibytes * BYTES_PER_MEBIBYTE);
        _streamSizeLimit = limitBytes;
        _log.LogInformation($"Multipart upload part size set to {limitInMebibytes} MiB.");
    }
}
