using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.Services;

public class HttpDownloadService
{
    private readonly OctoLogger _log;
    private readonly HttpClient _httpClient;
    private readonly FileSystemProvider _fileSystemProvider;

    public HttpDownloadService(OctoLogger log, HttpClient httpClient, FileSystemProvider fileSystemProvider, IVersionProvider versionProvider)
    {
        _log = log;
        _httpClient = httpClient;
        _fileSystemProvider = fileSystemProvider;

        if (_httpClient is not null)
        {
            _httpClient.Timeout = TimeSpan.FromHours(1);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", versionProvider?.GetCurrentVersion()));
            if (versionProvider?.GetVersionComments() is { } comments)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
            }
        }
    }

    public virtual async Task DownloadToFile(string url, string file)
    {
        _log.LogVerbose($"HTTP GET: {url}");

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");

        response.EnsureSuccessStatusCode();

        await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
        await using var streamToWriteTo = _fileSystemProvider.Open(file, FileMode.Create);
        await _fileSystemProvider.CopySourceToTargetStreamAsync(streamToReadFrom, streamToWriteTo);
    }

    public virtual async Task<byte[]> DownloadToBytes(string url)
    {
        _log.LogVerbose($"HTTP GET: {url}");

        using var response = await _httpClient.GetAsync(url);
        _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }
}
