using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class AdoClient
{
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;
    private double _retryDelay;
    private readonly RetryPolicy _retryPolicy;

    public AdoClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy, string personalAccessToken)
    {
        _log = log;
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;

        if (_httpClient != null)
        {
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", versionProvider?.GetCurrentVersion()));
            if (versionProvider?.GetVersionComments() is { } comments)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
            }
        }
    }

    public virtual async Task<string> GetAsync(string url)
    {
        return await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Get, url));
    }

    public virtual async Task<string> DeleteAsync(string url) => await SendAsync(HttpMethod.Delete, url);

    public virtual async Task<string> PostAsync(string url, object body) => await SendAsync(HttpMethod.Post, url, body);

    public virtual async Task<string> PutAsync(string url, object body) => await SendAsync(HttpMethod.Put, url, body);

    public virtual async Task<string> PatchAsync(string url, object body) => await SendAsync(HttpMethod.Patch, url, body);

    private async Task<string> SendAsync(HttpMethod httpMethod, string url, object body = null)
    {
        await ApplyRetryDelayAsync();
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
        CheckForRetryDelay(response);

        return content;
    }

    private async Task ApplyRetryDelayAsync()
    {
        if (_retryDelay > 0.0)
        {
            _log.LogWarning($"THROTTLING IN EFFECT. Waiting {(int)_retryDelay} ms");
            await Task.Delay((int)_retryDelay);
            _retryDelay = 0.0;
        }
    }

    public virtual async Task<JArray> GetWithPagingAsync(string url) => await GetWithPagingAsync(url, string.Empty);

    public virtual async Task<JArray> GetWithPagingAsync(string url, string continuationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            if (!url.Contains('?'))
            {
                url += "?";
            }
            else
            {
                url += "&";
            }

            url += $"continuationToken={continuationToken}";
        }

        await ApplyRetryDelayAsync();
        _log.LogVerbose($"HTTP GET: {url}");

        var response = await _retryPolicy.HttpRetry(async () =>
        {
            var httpResponse = await _httpClient.GetAsync(url);
            var httpContent = await httpResponse.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({httpResponse.StatusCode}): {httpContent}");
            httpResponse.EnsureSuccessStatusCode();
            return httpResponse;
        }, ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable);

        var content = await response.Content.ReadAsStringAsync();
        CheckForRetryDelay(response);

        var data = (JArray)JObject.Parse(content)["value"];

        if (response.Headers.Contains("x-ms-continuationtoken"))
        {
            var newToken = response.Headers.GetValues("x-ms-continuationtoken").Single();
            var continuationResults = await GetWithPagingAsync(url, newToken);

            foreach (var item in continuationResults)
            {
                data.Add(item);
            }
        }

        return data;
    }

    public virtual async Task<IEnumerable<T>> GetWithPagingTopSkipAsync<T>(string url, Func<JToken, T> selector) => await GetWithPagingTopSkipAsync(url, 0, selector);

    public virtual async Task<IEnumerable<T>> GetWithPagingTopSkipAsync<T>(string url, int skip, Func<JToken, T> selector)
    {
        if (url.IsNullOrWhiteSpace())
        {
            throw new ArgumentNullException(nameof(url));
        }

        var pageSize = 1000;
        var updatedUrl = url.Contains('?') ? url + "&" : url + "?";

        updatedUrl += $"$skip={skip}&$top={pageSize}";

        var content = await GetAsync(updatedUrl);

        var data = (JArray)JObject.Parse(content)["value"];
        var result = data.Select(selector);

        if (data.Count > 0)
        {
            var nextPages = await GetWithPagingTopSkipAsync(url, skip + pageSize, selector);

            result = result.Concat(nextPages);
        }

        return result;
    }

    public virtual async Task<int> GetCountUsingSkip(string url)
    {
        if (!await DoesSkipExist(url, 0))
        {
            return 0;
        }

        var minCount = 1;
        var maxCount = 500;

        while (await DoesSkipExist(url, maxCount))
        {
            maxCount *= 2;
        }

        var skip = 500;

        while (minCount < maxCount)
        {
            if (await DoesSkipExist(url, skip))
            {
                minCount = skip + 1;
            }
            else
            {
                maxCount = skip;
            }

            skip = ((maxCount - minCount) / 2) + minCount;
        }

        return minCount;
    }

    private async Task<bool> DoesSkipExist(string url, int skip)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (!url.Contains('?'))
        {
            url += "?";
        }
        else
        {
            url += "&";
        }

        url += $"$top=1&$skip={skip}";

        var content = await GetAsync(url);
        var count = (int)JObject.Parse(content)["count"];

        return count > 0;
    }

    private void CheckForRetryDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
        {
            _retryDelay = response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
        }
    }
}
