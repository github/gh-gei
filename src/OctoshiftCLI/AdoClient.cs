using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    public class AdoClient
    {
        private readonly string _adoToken;
        private readonly HttpClient _httpClient;
        private double _retryDelay = 0.0;

        public AdoClient(string adoToken)
        {
            _adoToken = adoToken;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _adoToken)));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        }

        public async Task<string> GetAsync(string url)
        {
            ApplyRetryDelay();
            var response = await _httpClient.GetAsync(url.Replace(" ", "%20"));
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostAsync(string url, HttpContent body)
        {
            ApplyRetryDelay();
            var response = await _httpClient.PostAsync(url.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PutAsync(string url, HttpContent body)
        {
            ApplyRetryDelay();
            var response = await _httpClient.PutAsync(url.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PatchAsync(string url, HttpContent body)
        {
            ApplyRetryDelay();
            var response = await _httpClient.PatchAsync(url.Replace(" ", "%20"), body);
            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            return await response.Content.ReadAsStringAsync();
        }

        private void ApplyRetryDelay()
        {
            if (_retryDelay > 0.0)
            {
                Console.WriteLine($"THROTTLING IN EFFECT. Waiting {(int)_retryDelay} ms");
                Thread.Sleep((int)_retryDelay);
                _retryDelay = 0.0;
            }
        }

        public async Task<JArray> GetWithPagingAsync(string url) => await GetWithPagingAsync(url, string.Empty);

        public async Task<JArray> GetWithPagingAsync(string url, string continuationToken)
        {
            var updatedUrl = url;

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
            var response = await _httpClient.GetAsync(updatedUrl.Replace(" ", "%20"));

            Console.WriteLine($"DEBUG: API Response from {updatedUrl.Replace(" ", "%20")}");
            var responseData = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseData);

            response.EnsureSuccessStatusCode();
            CheckForRetryDelay(response);

            var data = (JArray)JObject.Parse(responseData)["value"];

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
