using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class GitlabClient
{
    private const int DEFAULT_PAGE_SIZE = 100;
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;
    private readonly RetryPolicy _retryPolicy;
    private readonly FileSystemProvider _fileSystemProvider;

    public GitlabClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy, string gitlabPat, FileSystemProvider fileSystemProvider) :
        this(log, httpClient, versionProvider, retryPolicy, fileSystemProvider)
    {
        if (_httpClient != null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gitlabPat);
        }
    }

    public GitlabClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy, FileSystemProvider fileSystemProvider)
    {
        _log = log;
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;
        _fileSystemProvider = fileSystemProvider;

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
        using var response = await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Get, url));
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Like <see cref="GetAsync(string)"/>, but returns <c>null</c> when the server responds with 404 Not Found
    /// instead of throwing. Other HTTP errors are still retried and bubble up as exceptions.
    /// </summary>
    public virtual async Task<string> GetOrNullForNotFoundAsync(string url)
    {
        try
        {
            using var response = await _retryPolicy.HttpRetry(
                async () => await SendAsync(HttpMethod.Get, url),
                ex => ex.StatusCode != HttpStatusCode.NotFound);
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async IAsyncEnumerable<JToken> GetAllAsync(string url)
    {
        var nextPage = 1;
        while (nextPage > 0)
        {
            using var response = await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Get, AddPageParam(url, nextPage)));
            var content = await response.Content.ReadAsStringAsync();
            var jArray = JArray.Parse(content);

            foreach (var jToken in jArray)
            {
                yield return jToken;
            }

            nextPage = response.Headers.TryGetValues("X-Next-Page", out var values)
                && int.TryParse(values.FirstOrDefault(), out var parsed)
                ? parsed
                : 0;
        }
    }

    public virtual async Task<HttpResponseMessage> GetAsyncHttpResponseMessage(string url)
    {
        return await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Get, url));
    }

    public virtual async Task<string> PostAsync(string url, object body)
    {
        using var response = await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Post, url, body));
        return await response.Content.ReadAsStringAsync();
    }

    public virtual async Task<string> DeleteAsync(string url)
    {
        using var response = await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Delete, url));
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod httpMethod, string url, object body = null)
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

        return response;
    }

    private static string AddPageParam(string url, int page)
    {
        var uri = new Uri(url);
        var path = uri.GetLeftPart(UriPartial.Path);
        var queryParams = HttpUtility.ParseQueryString(uri.Query);

        queryParams["page"] = page.ToString();
        if (string.IsNullOrEmpty(queryParams["per_page"]))
        {
            queryParams["per_page"] = DEFAULT_PAGE_SIZE.ToString();
        }

        return $"{path}?{queryParams}";
    }

    public virtual async Task DownloadToFile(string url, string file)
    {
        _log.LogVerbose($"HTTP GET: {url}");

        await _retryPolicy.Retry(async () =>
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds
                                 ?? (response.Headers.RetryAfter?.Date.HasValue == true
                                     ? (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds
                                     : (double?)null);
                var retryAfterMessage = retryAfter.HasValue ? $" GitLab requested a retry after {Math.Max(0, (int)retryAfter.Value)} seconds." : "";
                _log.LogWarning($"GitLab rate limit hit (HTTP 429) downloading the export archive.{retryAfterMessage} Retrying...");

                if (retryAfter is > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(retryAfter.Value, 60)));
                }
            }

            response.EnsureSuccessStatusCode();

            await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
            await using var streamToWriteTo = _fileSystemProvider.Open(file, FileMode.Create);
            await _fileSystemProvider.CopySourceToTargetStreamAsync(streamToReadFrom, streamToWriteTo);
        });
    }
}
