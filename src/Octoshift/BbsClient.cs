using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI;

public class BbsClient
{
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;
    private readonly RetryPolicy _retryPolicy;

    public BbsClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy, string username, string password) :
        this(log, httpClient, versionProvider, retryPolicy)
    {
        if (_httpClient != null)
        {
            var authCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authCredentials);
        }
    }

    public BbsClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy)
    {
        _log = log;
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;

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

    public virtual async Task<string> GetAsync(string url)
    {
        return await _retryPolicy.HttpRetry(async () => await SendAsync(HttpMethod.Get, url), _ => true);
    }

    public virtual async Task<string> PostAsync(string url, object body) => await SendAsync(HttpMethod.Post, url, body);

    public virtual async Task<string> DeleteAsync(string url) => await SendAsync(HttpMethod.Delete, url);

    private async Task<string> SendAsync(HttpMethod httpMethod, string url, object body = null)
    {
        _log.LogVerbose($"HTTP {httpMethod}: {url}");

        if (body != null)
        {
            _log.LogVerbose($"HTTP BODY: {body.ToJson()}");
        }

        using var payload = body?.ToJson().ToStringContent();
        var response = httpMethod.ToString() switch
        {
            "GET" => await _httpClient.GetAsync(url),
            "DELETE" => await _httpClient.DeleteAsync(url),
            "POST" => await _httpClient.PostAsync(url, payload),
            "PUT" => await _httpClient.PutAsync(url, payload),
            "PATCH" => await _httpClient.PatchAsync(url, payload),
            _ => throw new ArgumentOutOfRangeException($"{httpMethod} is not supported.")
        };
        var content = await response.Content.ReadAsStringAsync();
        _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");

        response.EnsureSuccessStatusCode();

        return content;

    }
}
