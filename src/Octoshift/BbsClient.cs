using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI
{
    public class BbsClient
    {
        private readonly HttpClient _httpClient;

        public BbsClient(HttpClient httpClient, IVersionProvider versionProvider, string personalAccessToken)
        {
            _httpClient = httpClient;

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
    }
}
