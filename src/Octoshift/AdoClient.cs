using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public class AdoClient
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;
        private double _retryDelay;

        public AdoClient(OctoLogger log, HttpClient httpClient, string personalAccessToken)
        {
            _log = log;
            _httpClient = httpClient;

            if (_httpClient != null)
            {
                _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            }
        }

        public virtual async Task<string> GetAsync(string url) => await SendAsync(HttpMethod.Get, url);

        public virtual async Task<string> DeleteAsync(string url) => await SendAsync(HttpMethod.Delete, url);

        public virtual async Task<string> PostAsync(string url, object body) => await SendAsync(HttpMethod.Post, url, body);

        public virtual async Task<string> PutAsync(string url, object body) => await SendAsync(HttpMethod.Put, url, body);

        public virtual async Task<string> PatchAsync(string url, object body) => await SendAsync(HttpMethod.Patch, url, body);

        private async Task<string> SendAsync(HttpMethod httpMethod, string url, object body = null)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
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

        private void ApplyRetryDelay()
        {
            if (_retryDelay > 0.0)
            {
                _log.LogWarning($"THROTTLING IN EFFECT. Waiting {(int)_retryDelay} ms");
                Thread.Sleep((int)_retryDelay);
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

            var updatedUrl = url.Replace(" ", "%20");

            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                if (!updatedUrl.Contains("?"))
                {
                    updatedUrl += "?";
                }
                else
                {
                    updatedUrl += "&";
                }

                updatedUrl += $"continuationToken={continuationToken}";
            }

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP GET: {url}");
            var response = await _httpClient.GetAsync(updatedUrl);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
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

        private void CheckForRetryDelay(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
            {
                _retryDelay = response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
            }
        }
    }
}