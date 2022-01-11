using System;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public class GithubClient
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;

        public GithubClient(OctoLogger log, HttpClient httpClient)
        {
            _log = log;
            _httpClient = httpClient;
        }

        public virtual async Task<string> GetAsync(string url) => await SendAsync(HttpMethod.Get, url);

        public virtual async Task<string> PostAsync(string url, object body) =>
            await SendAsync(HttpMethod.Post, url, body);

        public virtual async Task<string> PutAsync(string url, object body) =>
            await SendAsync(HttpMethod.Put, url, body);

        public virtual async Task<string> PatchAsync(string url, object body) =>
            await SendAsync(HttpMethod.Patch, url, body);

        public virtual async Task<string> DeleteAsync(string url) => await SendAsync(HttpMethod.Delete, url);

        private async Task<string> SendAsync(HttpMethod httpMethod, string url, object body = null)
        {
            url = url?.Replace(" ", "%20");

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
}