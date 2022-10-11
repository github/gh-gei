using System.Net.Http;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public class FileDownloader
{
    private readonly HttpClient _client;
    private readonly OctoLogger _octoLogger;

    public FileDownloader(HttpClient client, OctoLogger octoLogger)
    {
        _client = client;
        _octoLogger = octoLogger;
    }

    public virtual async Task<byte[]> DownloadArchive(string fromUrl)
    {
        _octoLogger.LogVerbose($"HTTP GET: {fromUrl}");
        using var response = await _client.GetAsync(fromUrl);
        _octoLogger.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }
}