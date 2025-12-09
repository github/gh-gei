using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class GithubClient
{
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;
    private int _retryDelay;
    private readonly RetryPolicy _retryPolicy;
    private readonly DateTimeProvider _dateTimeProvider;

    private const string DEFAULT_RATE_LIMIT_REMAINING = "5000";
    private const int MILLISECONDS_PER_SECOND = 1000;
    private const int SECONDARY_RATE_LIMIT_MAX_RETRIES = 3;
    private const int SECONDARY_RATE_LIMIT_DEFAULT_DELAY = 60; // 60 seconds default delay

    public GithubClient(OctoLogger log, HttpClient httpClient, IVersionProvider versionProvider, RetryPolicy retryPolicy, DateTimeProvider dateTimeProvider, string personalAccessToken)
    {
        _log = log;
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;
        _dateTimeProvider = dateTimeProvider;

        if (_httpClient != null)
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.DefaultRequestHeaders.Add("GraphQL-Features", "import_api,mannequin_claiming_emu,org_import_api");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", versionProvider?.GetCurrentVersion()));
            if (versionProvider?.GetVersionComments() is { } comments)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
            }
        }
    }

    public virtual async Task<string> GetNonSuccessAsync(string url, HttpStatusCode status) => (await GetWithRetry(url, expectedStatus: status)).Content;

    public virtual async Task<string> GetAsync(string url, Dictionary<string, string> customHeaders = null) => (await GetWithRetry(url, customHeaders)).Content;

    public virtual IAsyncEnumerable<JToken> GetAllAsync(string url, Dictionary<string, string> customHeaders = null) =>
        GetAllAsync(url, jToken => (JArray)jToken, customHeaders);

    public virtual async IAsyncEnumerable<JToken> GetAllAsync(
        string url,
        Func<JToken, JArray> resultCollectionSelector,
        Dictionary<string, string> customHeaders = null)
    {
        if (resultCollectionSelector is null)
        {
            throw new ArgumentNullException(nameof(resultCollectionSelector));
        }

        var nextUrl = url;
        do
        {
            var (content, headers) = await GetWithRetry(nextUrl, customHeaders: customHeaders);
            var jContent = JToken.Parse(content);
            foreach (var jToken in resultCollectionSelector(jContent))
            {
                yield return jToken;
            }

            nextUrl = GetNextUrl(headers);
        } while (nextUrl != null);
    }
    public virtual async Task<string> PostAsync(string url, object body, Dictionary<string, string> customHeaders = null) =>
        (await SendAsync(HttpMethod.Post, url, body, customHeaders: customHeaders)).Content;

    public virtual async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> PostWithFullResponseAsync(string url, object body, Dictionary<string, string> customHeaders = null) =>
        await SendAsync(HttpMethod.Post, url, body, customHeaders: customHeaders);

    public virtual async Task<JToken> PostGraphQLAsync(
        string url,
        object body,
        Dictionary<string, string> customHeaders = null)
    {
        var response = await PostAsync(url, body, customHeaders);
        var data = JObject.Parse(response);
        EnsureSuccessGraphQLResponse(data);

        return data;
    }

    public virtual async IAsyncEnumerable<JToken> PostGraphQLWithPaginationAsync(
        string url,
        object body,
        Func<JObject, JArray> resultCollectionSelector,
        Func<JObject, JObject> pageInfoSelector,
        int first = 100,
        string after = null,
        Dictionary<string, string> customHeaders = null)
    {
        if (resultCollectionSelector is null)
        {
            throw new ArgumentNullException(nameof(resultCollectionSelector));
        }

        if (pageInfoSelector is null)
        {
            throw new ArgumentNullException(nameof(pageInfoSelector));
        }

        var jBody = JObject.FromObject(body);
        jBody["variables"] ??= new JObject();
        jBody["variables"]["first"] = first;

        var hasNextPage = true;
        while (hasNextPage)
        {
            jBody["variables"]["after"] = after;

            var (content, _) = await SendAsync(HttpMethod.Post, url, jBody, customHeaders: customHeaders);
            var jContent = JObject.Parse(content);
            foreach (var jResult in resultCollectionSelector(jContent))
            {
                yield return jResult;
            }

            var pageInfo = pageInfoSelector(jContent);
            if (pageInfo is null)
            {
                yield break;
            }

            hasNextPage = pageInfo["hasNextPage"]?.ToObject<bool>() ?? false;
            after = pageInfo["endCursor"]?.ToObject<string>();
        }
    }

    public virtual async Task<string> PutAsync(string url, object body, Dictionary<string, string> customHeaders = null) =>
        (await SendAsync(HttpMethod.Put, url, body, customHeaders: customHeaders)).Content;

    public virtual async Task<string> PatchAsync(string url, object body, Dictionary<string, string> customHeaders = null) =>
        (await SendAsync(HttpMethod.Patch, url, body, customHeaders: customHeaders)).Content;

    public virtual async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> PatchWithFullResponseAsync(string url, object body, Dictionary<string, string> customHeaders = null) =>
        await SendAsync(HttpMethod.Patch, url, body, customHeaders: customHeaders);

    public virtual async Task<string> DeleteAsync(string url, Dictionary<string, string> customHeaders = null) => (await SendAsync(HttpMethod.Delete, url, customHeaders: customHeaders)).Content;

    private async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> GetWithRetry(
        string url,
        Dictionary<string, string> customHeaders = null,
        HttpStatusCode expectedStatus = HttpStatusCode.OK) =>
        await _retryPolicy.Retry(async () => await SendAsync(HttpMethod.Get, url, customHeaders: customHeaders, expectedStatus: expectedStatus));

    private async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> SendAsync(
        HttpMethod httpMethod,
        string url,
        object body = null,
        HttpStatusCode expectedStatus = HttpStatusCode.OK,
        Dictionary<string, string> customHeaders = null,
        int retryCount = 0)
    {
        await ApplyRetryDelayAsync();
        _log.LogVerbose($"HTTP {httpMethod}: {url}");

        using var request = new HttpRequestMessage(httpMethod, url).AddHeaders(customHeaders);

        if (body != null)
        {
            if (body is HttpContent httpContent)
            {
                _log.LogVerbose("HTTP BODY: BLOB");
                request.Content = httpContent;
            }
            else
            {
                var jsonBody = body.ToJson();
                _log.LogVerbose($"HTTP BODY: {jsonBody}");
                request.Content = jsonBody.ToStringContent();
            }
        }

        using var response = await _httpClient.SendAsync(request);

        _log.LogVerbose($"GITHUB REQUEST ID: {ExtractHeaderValue("X-GitHub-Request-Id", response.Headers)}");
        var content = await response.Content.ReadAsStringAsync();
        _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");

        var headers = response.Headers.ToArray();

        foreach (var header in headers)
        {
            _log.LogDebug($"RESPONSE HEADER: {header.Key} = {string.Join(",", header.Value)}");
        }

        if (GetRateLimitRemaining(headers) <= 0 && content.ToUpper().Contains("API RATE LIMIT EXCEEDED"))
        {
            SetRetryDelay(headers);
        }

        // Check for secondary rate limits before handling primary rate limits
        if (IsSecondaryRateLimit(response.StatusCode, content))
        {
            return await HandleSecondaryRateLimit(httpMethod, url, body, expectedStatus, customHeaders, headers, retryCount);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden && _retryDelay > 0)
        {
            (content, headers) = await SendAsync(httpMethod, url, body, expectedStatus, customHeaders, retryCount);
        }
        else if (expectedStatus == HttpStatusCode.OK)
        {
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"GitHub API error: {content}", ex, response.StatusCode);
            }
        }
        else if (response.StatusCode != expectedStatus)
        {
            throw new HttpRequestException($"Expected status code {expectedStatus} but got {response.StatusCode}", null, response.StatusCode);
        }

        return (content, headers);
    }

    private async Task ApplyRetryDelayAsync()
    {
        if (_retryDelay > 0)
        {
            _log.LogWarning($"GitHub rate limit exceeded. Waiting {_retryDelay} seconds before continuing");
            await Task.Delay(_retryDelay * MILLISECONDS_PER_SECOND);
            _retryDelay = 0;
        }
    }

    private string GetNextUrl(KeyValuePair<string, IEnumerable<string>>[] headers)
    {
        var linkHeaderValue = ExtractLinkHeader(headers);

        var nextUrl = linkHeaderValue?
            .Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(link =>
            {
                var rx = new Regex(@"<(?<url>.+)>;\s*rel=""(?<rel>.+)""");
                var url = rx.Match(link).Groups["url"].Value;
                var rel = rx.Match(link).Groups["rel"].Value; // first, next, last, prev

                return (Url: url, Rel: rel);
            })
            .FirstOrDefault(x => x.Rel == "next").Url;

        return nextUrl;
    }

    private string ExtractLinkHeader(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        ExtractHeaderValue("Link", headers);

    private int GetRateLimitRemaining(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        int.Parse(ExtractHeaderValue("X-RateLimit-Remaining", headers) ?? DEFAULT_RATE_LIMIT_REMAINING);

    private long GetRateLimitReset(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        long.Parse(ExtractHeaderValue("X-RateLimit-Reset", headers) ?? _dateTimeProvider.CurrentUnixTimeSeconds().ToString());

    private string ExtractHeaderValue(string key, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        headers.SingleOrDefault(kvp => kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();

    private void SetRetryDelay(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var resetUnixSeconds = GetRateLimitReset(headers);
        var currentUnixSeconds = _dateTimeProvider.CurrentUnixTimeSeconds();

        _retryDelay = (int)(resetUnixSeconds - currentUnixSeconds);
    }

    private void EnsureSuccessGraphQLResponse(JObject response)
    {
        if (response.TryGetValue("errors", out var jErrors) && jErrors is JArray { Count: > 0 } errors)
        {
            var error = (JObject)errors[0];
            var errorMessage = error.TryGetValue("message", out var jMessage) ? (string)jMessage : null;
            throw new OctoshiftCliException($"{errorMessage ?? "UNKNOWN"}");
        }
    }

    private bool IsSecondaryRateLimit(HttpStatusCode statusCode, string content)
    {
        // Secondary rate limits return 403 or 429
        if (statusCode is not HttpStatusCode.Forbidden and not HttpStatusCode.TooManyRequests)
        {
            return false;
        }

        // Check if this is a primary rate limit (which we handle separately)
        if (content.ToUpper().Contains("API RATE LIMIT EXCEEDED"))
        {
            return false;
        }

        // Common secondary rate limit error patterns
        var contentUpper = content.ToUpper();
        return contentUpper.Contains("SECONDARY RATE LIMIT") ||
               contentUpper.Contains("ABUSE DETECTION") ||
               contentUpper.Contains("YOU HAVE TRIGGERED AN ABUSE DETECTION MECHANISM") ||
               statusCode == HttpStatusCode.TooManyRequests;
    }

    private async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> HandleSecondaryRateLimit(
        HttpMethod httpMethod,
        string url,
        object body,
        HttpStatusCode expectedStatus,
        Dictionary<string, string> customHeaders,
        KeyValuePair<string, IEnumerable<string>>[] headers,
        int retryCount = 0)
    {
        if (retryCount >= SECONDARY_RATE_LIMIT_MAX_RETRIES)
        {
            throw new OctoshiftCliException($"Secondary rate limit exceeded. Maximum retries ({SECONDARY_RATE_LIMIT_MAX_RETRIES}) reached. Please wait before retrying your request.");
        }

        var delaySeconds = GetSecondaryRateLimitDelay(headers, retryCount);

        _log.LogWarning($"Secondary rate limit detected (attempt {retryCount + 1}/{SECONDARY_RATE_LIMIT_MAX_RETRIES}). Waiting {delaySeconds} seconds before retrying...");

        await Task.Delay(delaySeconds * MILLISECONDS_PER_SECOND);

        return await SendAsync(httpMethod, url, body, expectedStatus, customHeaders, retryCount + 1);
    }

    private int GetSecondaryRateLimitDelay(KeyValuePair<string, IEnumerable<string>>[] headers, int retryCount)
    {
        // First check for retry-after header
        var retryAfterHeader = ExtractHeaderValue("Retry-After", headers);
        if (!string.IsNullOrEmpty(retryAfterHeader) && int.TryParse(retryAfterHeader, out var retryAfterSeconds))
        {
            return retryAfterSeconds;
        }

        // Then check if x-ratelimit-remaining is 0 and use x-ratelimit-reset
        var rateLimitRemaining = GetRateLimitRemaining(headers);
        if (rateLimitRemaining <= 0)
        {
            var resetUnixSeconds = GetRateLimitReset(headers);
            var currentUnixSeconds = _dateTimeProvider.CurrentUnixTimeSeconds();
            var delayFromReset = (int)(resetUnixSeconds - currentUnixSeconds);

            if (delayFromReset > 0)
            {
                return delayFromReset;
            }
        }

        // Otherwise use exponential backoff: 1m → 2m → 4m
        return SECONDARY_RATE_LIMIT_DEFAULT_DELAY * (int)Math.Pow(2, retryCount);
    }
}
