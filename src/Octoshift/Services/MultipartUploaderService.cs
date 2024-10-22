using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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

        // 1. Start the upload
        var startHeaders = await StartUploadAsync(uploadUrl, archiveName, archiveContent.Length);

        var nextUrl = GetNextUrl(startHeaders);
        var guid = System.Web.HttpUtility.ParseQueryString(nextUrl.Query)["guid"];

        // 2. Upload parts
        int bytesRead;
        while ((bytesRead = await archiveContent.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            nextUrl = await UploadPartAsync(buffer, bytesRead, nextUrl.ToString());
        }

        // 3. Complete the upload
        await CompleteUploadAsync(nextUrl.ToString());

        return $"gei://archive/{guid}";
    }

    private async Task<IEnumerable<KeyValuePair<string, IEnumerable<string>>>> StartUploadAsync(string uploadUrl, string archiveName, long contentSize)
    {
        var body = new JObject
        {
            ["content_type"] = "application/octet-stream",
            ["name"] = archiveName,
            ["size"] = contentSize
        };

        var response = await _client.PostTupleAsync(uploadUrl, body);
        var headers = response.ResponseHeaders.ToList();
        return headers;
    }

    private async Task<Uri> UploadPartAsync(byte[] body, int bytesRead, string nextUrl)
    {
        var content = new ByteArrayContent(body, 0, bytesRead);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var patchResponse = await _client.PatchTupleAsync(nextUrl, content);
        var headers = patchResponse.ResponseHeaders.ToList();

        return GetNextUrl(headers);
    }

    private async Task CompleteUploadAsync(string lastUrl)
    {
        var content = new StringContent(string.Empty);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        await _client.PutAsync(lastUrl, content);
    }

    private Uri GetNextUrl(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var locationHeader = headers.FirstOrDefault(header => header.Key == "Location");

        if (locationHeader.Key != null)
        {
            var locationValue = locationHeader.Value.FirstOrDefault();
            if (!string.IsNullOrEmpty(locationValue))
            {
                var nextUrl = new Uri(new Uri(_base_url), locationValue);
                return nextUrl;
            }
        }

        return null;
    }
}
