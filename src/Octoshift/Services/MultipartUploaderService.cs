using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class ArchiveUploader
{
    private readonly GithubClient _client;
    private readonly OctoLogger _log;
    internal int _streamSizeLimit = 100 * 1024 * 1024; // 100 MiB

    private const string BASE_URL = "https://uploads.github.com/organizations";

    public ArchiveUploader(GithubClient client, OctoLogger log)
    {
        _client = client;
        _log = log;
    }
    public virtual async Task<string> Upload(Stream archiveContent, string archiveName, string orgDatabaseId)
    {
        using var streamContent = new StreamContent(archiveContent);
        streamContent.Headers.ContentType = new("application/octet-stream");

        var isMultipart = archiveContent.Length > _streamSizeLimit; // Determines if stream size is greater than 100MB

        string response;

        if (isMultipart)
        {
            var url = $"{BASE_URL}/{orgDatabaseId.EscapeDataString()}/gei/archive/blobs/uploads";

            response = await UploadMultipart(archiveContent, archiveName, url);
            return response;
        }
        else
        {
            var url = $"{BASE_URL}/{orgDatabaseId.EscapeDataString()}/gei/archive?name={archiveName.EscapeDataString()}";

            response = await _client.PostAsync(url, streamContent);
            var data = JObject.Parse(response);
            return (string)data["uri"];
        }
    }

    public async Task<string> UploadMultipart(Stream archiveContent, string archiveName, string uploadUrl)
    {
        if (archiveContent == null)
        {
            throw new ArgumentNullException(nameof(archiveContent), "Archive content stream cannot be null.");
        }

        var buffer = new byte[_streamSizeLimit];

        try
        {
            // 1. Start the upload
            var startHeaders = await StartUpload(uploadUrl, archiveName, archiveContent.Length);

            var nextUrl = GetNextUrl(startHeaders);
            if (nextUrl == null)
            {
                throw new OctoshiftCliException("Failed to retrieve the next URL for the upload.");
            }

            var guid = HttpUtility.ParseQueryString(nextUrl.Query)["guid"];

            // 2. Upload parts
            int bytesRead;
            var partsRead = 0;
            var totalParts = (long)Math.Ceiling((double)archiveContent.Length / _streamSizeLimit);
            while ((bytesRead = await archiveContent.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                nextUrl = await UploadPart(buffer, bytesRead, nextUrl.ToString(), partsRead, totalParts);
                partsRead++;
            }

            // 3. Complete the upload
            await CompleteUpload(nextUrl.ToString());

            return $"gei://archive/{guid}";
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed during multipart upload.", ex);
        }
    }

    private async Task<IEnumerable<KeyValuePair<string, IEnumerable<string>>>> StartUpload(string uploadUrl, string archiveName, long contentSize)
    {
        _log.LogInformation("Starting archive upload into GitHub owned storage...");

        var body = new
        {
            content_type = "application/octet-stream",
            name = archiveName,
            size = contentSize
        };

        try
        {
            var response = await _client.PostWithFullResponseAsync(uploadUrl, body);
            return response.ResponseHeaders.ToList();
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to start upload.", ex);
        }
    }

    private async Task<Uri> UploadPart(byte[] body, int bytesRead, string nextUrl, int partsRead, int totalParts)
    {
        _log.LogInformation($"Uploading part {partsRead + 1}/{totalParts}...");
        using var content = new ByteArrayContent(body, 0, bytesRead);
        content.Headers.ContentType = new("application/octet-stream");

        try
        {
            // Make the PATCH request and retrieve headers
            var patchResponse = await _client.PatchWithFullResponseAsync(nextUrl, content);
            var headers = patchResponse.ResponseHeaders.ToList();

            // Retrieve the next URL from the response headers
            return GetNextUrl(headers);
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to upload part.", ex);
        }
    }

    private async Task CompleteUpload(string lastUrl)
    {
        try
        {
            await _client.PutAsync(lastUrl, "");
            _log.LogInformation("Finished uploading archive");
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
                return new Uri(new Uri(BASE_URL), locationValue);
            }
        }
        throw new OctoshiftCliException("Location header is missing in the response, unable to retrieve next URL for multipart upload.");
    }
}
