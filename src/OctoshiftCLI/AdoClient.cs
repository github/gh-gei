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
    public class AdoClient : IDisposable
    {
        private readonly string _adoToken;
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;
        private double _retryDelay;
        private bool disposedValue;

        public AdoClient(OctoLogger log, string adoToken)
        {
            _log = log;
            _adoToken = adoToken;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _adoToken)));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
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

        public virtual async Task<string> PostAsync(string url, HttpContent body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP POST: {url}");
            _log.LogVerbose($"HTTP BODY: {await body?.ReadAsStringAsync()}");
            var response = await _httpClient.PostAsync(url, body);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return content;
        }

        public virtual async Task<string> PutAsync(string url, HttpContent body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP PUT: {url}");
            _log.LogVerbose($"HTTP BODY: {await body?.ReadAsStringAsync()}");
            var response = await _httpClient.PutAsync(url, body);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return content;
        }

        public virtual async Task<string> PatchAsync(string url, HttpContent body)
        {
            url = url?.Replace(" ", "%20");

            ApplyRetryDelay();
            _log.LogVerbose($"HTTP PATCH: {url}");
            _log.LogVerbose($"HTTP BODY: {await body?.ReadAsStringAsync()}");
            var response = await _httpClient.PatchAsync(url, body);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}