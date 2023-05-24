using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.Services;

public class BasicHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;

    public BasicHttpClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider)
    {
        _log = log;
        _httpClient = httpClient;

        if (_httpClient != null)
        {
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", versionProvider?.GetCurrentVersion()));
            if (versionProvider?.GetVersionComments() is { } comments)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
            }
        }
    }

    public async Task<string> GetAsync(string url)
    {
        _log.LogVerbose($"HTTP GET: {url}");

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");

        response.EnsureSuccessStatusCode();

        return content;
    }
}
