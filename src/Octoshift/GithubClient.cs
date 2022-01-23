using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public class GithubClient
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;

        public GithubClient(OctoLogger log, HttpClient httpClient, string personalAccessToken)
        {
            _log = log;
            _httpClient = httpClient;

            if (_httpClient != null)
            {
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", "0.1"));
                _httpClient.DefaultRequestHeaders.Add("GraphQL-Features", "import_api");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
            }
        }

        public virtual async Task<string> GetAsync(string url) => (await SendAsync(HttpMethod.Get, url)).Content;

        public virtual async IAsyncEnumerable<string> GetAllAsync(string url)
        {
            var (content, headers) = await SendAsync(HttpMethod.Get, url);
            yield return content;

            var nextUrl = GetNextUrl(headers);
            while (nextUrl != null)
            {
                (content, headers) = await SendAsync(HttpMethod.Get, nextUrl);
                yield return content;

                nextUrl = GetNextUrl(headers);
            }
        }

        public virtual async Task<string> PostAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Post, url, body)).Content;

        public virtual async Task<string> PutAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Put, url, body)).Content;

        public virtual async Task<string> PatchAsync(string url, object body) =>
            (await SendAsync(HttpMethod.Patch, url, body)).Content;

        public virtual async Task<string> DeleteAsync(string url) => (await SendAsync(HttpMethod.Delete, url)).Content;

        private async Task<(string Content, KeyValuePair<string, IEnumerable<string>>[] ResponseHeaders)> SendAsync(
            HttpMethod httpMethod, string url, object body = null)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP {httpMethod}: {url}");

            if (body != null)
            {
                _log.LogVerbose($"HTTP BODY: {body.ToJson()}");
            }

            using var payload = body?.ToJson().ToStringContent();
            using var response = httpMethod.ToString() switch
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

            return (content, response.Headers.ToArray());
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

        private string ExtractLinkHeader(KeyValuePair<string, IEnumerable<string>>[] headers) =>
            headers.SingleOrDefault(kvp => kvp.Key == "Link").Value?.FirstOrDefault();
    }
}