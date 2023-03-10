using System;
using System.Net.Http;
using System.Net.Http.Headers;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI
{
    public sealed class HttpDownloadServiceFactory : IHttpDownloadServiceFactory
    {
        private readonly OctoLogger _log;
        private readonly IHttpClientFactory _clientFactory;
        private readonly FileSystemProvider _fileSystemProvider;
        private readonly IVersionProvider _versionProvider;

        public HttpDownloadServiceFactory(OctoLogger log, IHttpClientFactory clientFactory, FileSystemProvider fileSystemProvider, IVersionProvider versionProvider)
        {
            _log = log;
            _clientFactory = clientFactory;
            _fileSystemProvider = fileSystemProvider;
            _versionProvider = versionProvider;
        }

        public HttpDownloadService Create(string apiUrl = null, string sourcePersonalAccessToken = null)
        {
            var httpClient = _clientFactory.CreateClient("Default");

            ConfigureClient(httpClient);

            return new HttpDownloadService(_log, httpClient, _fileSystemProvider);
        }

        public HttpDownloadService CreateClientNoSsl(string apiUrl = null, string sourcePersonalAccessToken = null)
        {
            var httpClient = _clientFactory.CreateClient("NoSSL");

            ConfigureClient(httpClient);

            return new HttpDownloadService(_log, httpClient, _fileSystemProvider);
        }

        private void ConfigureClient(HttpClient httpClient)
        {
            if (httpClient is not null)
            {
                httpClient.Timeout = TimeSpan.FromHours(1);
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", _versionProvider?.GetCurrentVersion()));
                if (_versionProvider?.GetVersionComments() is { } comments)
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
                }
            }
        }
    }
}

