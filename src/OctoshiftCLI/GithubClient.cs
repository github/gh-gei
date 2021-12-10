using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    public class GithubClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly OctoLogger _log;
        private bool disposedValue;

        public GithubClient(OctoLogger log, string githubToken)
        {
            _log = log;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", "0.1"));
            _httpClient.DefaultRequestHeaders.Add("GraphQL-Features", "import_api");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        }

        public virtual async Task<string> GetAsync(string url)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP GET: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();

            return content;
        }

        public virtual async Task<string> PostAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP GET: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var payload = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, payload);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();

            return content;
        }

        public virtual async Task<string> PutAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP GET: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var payload = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, payload);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();

            return content;
        }

        public virtual async Task<string> PatchAsync(string url, string body)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP GET: {url}");
            _log.LogVerbose($"HTTP BODY: {body}");
            using var payload = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(url, payload);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();

            return content;
        }

        public virtual async Task<string> DeleteAsync(string url)
        {
            url = url?.Replace(" ", "%20");

            _log.LogVerbose($"HTTP GET: {url}");
            var response = await _httpClient.DeleteAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            response.EnsureSuccessStatusCode();

            return content;
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