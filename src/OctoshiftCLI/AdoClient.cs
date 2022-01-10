using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI
{
    public class AdoClient
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;
        private readonly EnvironmentVariableProvider _env;
        private bool _httpClientInitialized;
        private double _retryDelay;

        public AdoClient(OctoLogger log, HttpClient httpClient, EnvironmentVariableProvider env)
        {
            _log = log;
            _httpClient = httpClient;
            _env = env;
        }

        public HttpClient GetHttpClient()
        {
            if (!_httpClientInitialized)
            {
                _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

                var adoToken = _env.AdoPersonalAccessToken();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", adoToken)));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                _httpClientInitialized = true;
            }

            return _httpClient;
        }

        public virtual async Task<string> GetAsync(string url)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP GET: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return content;
        }

        public virtual async Task<string> PostAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP POST: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var bodyContent = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, bodyContent);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return content;
        }

        public virtual async Task<string> PutAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP PUT: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var bodyContent = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, bodyContent);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return content;
        }

        public virtual async Task<string> PatchAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP PATCH: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var bodyContent = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(url, bodyContent);
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