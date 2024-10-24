using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace OctoshiftCLI.Services;

public class MultipartUploaderService
{
    private readonly GithubClient _client;
    internal int _streamSizeLimit = 100 * 1024 * 1024; // 100 MiB
    internal string _base_url = "https://uploads.github.com/organizations";

    public MultipartUploaderService(GithubClient client)
    {
        _client = client;
    }

    public async Task<string> UploadMultipart(Stream archiveContent, string archiveName, string uploadUrl)
    {
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
            while ((bytesRead = await archiveContent.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                nextUrl = await UploadPart(buffer, bytesRead, nextUrl.ToString());
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
        var body = new
        {
            content_type = "application/octet-stream",
            name = archiveName,
            size = contentSize
        };

        try
        {
            // Post with the expectation of receiving both response content and headers
            var response = await _client.PostWithFullResponseAsync(uploadUrl, body);
            return response.ResponseHeaders.ToList();
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to start upload.", ex);
        }
    }

    private async Task<Uri> UploadPart(byte[] body, int bytesRead, string nextUrl)
    {
        var content = new ByteArrayContent(body, 0, bytesRead);
        content.Headers.ContentType = new("application/octet-stream");

        try
        {
            // Make the PATCH request and retrieve headers
            var patchResponse = await _client.PatchWithFullResponseAsync(nextUrl, content);
            var headers = patchResponse.ResponseHeaders.ToList();

            // Retrieve the next URL from the response headers
            return GetNextUrl(headers) ?? throw new OctoshiftCliException("Failed to retrieve the next URL for the upload part.");
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to upload part.", ex);
        }
    }

    private async Task CompleteUpload(string lastUrl)
    {
        var content = new StringContent(string.Empty);
        content.Headers.ContentType = new("application/octet-stream");

        try
        {
            await _client.PutAsync(lastUrl, content);
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException("Failed to complete upload.", ex);
        }
    }

    private Uri GetNextUrl(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        // Use FirstOrDefault to safely handle missing Location headers
        var locationHeader = headers.FirstOrDefault(header => header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(locationHeader.Key))
        {
            var locationValue = locationHeader.Value.FirstOrDefault();
            if (!string.IsNullOrEmpty(locationValue))
            {
                return new Uri(new Uri(_base_url), locationValue);
            }
        }
        return null;
    }
}
