using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    public class GithubClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool disposedValue;

        public GithubClient(string githubToken)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", "0.1"));
            _httpClient.DefaultRequestHeaders.Add("GraphQL-Features", "import_api");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        }

        public async Task<string> GetAsync(string url)
        {
            var response = await _httpClient.GetAsync(url?.Replace(" ", "%20"));
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostAsync(string url, HttpContent body)
        {
            var response = await _httpClient.PostAsync(url?.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PutAsync(string url, HttpContent body)
        {
            var response = await _httpClient.PutAsync(url?.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PatchAsync(string url, HttpContent body)
        {
            var response = await _httpClient.PatchAsync(url?.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> DeleteAsync(string url)
        {
            var response = await _httpClient.DeleteAsync(url?.Replace(" ", "%20"));
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
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