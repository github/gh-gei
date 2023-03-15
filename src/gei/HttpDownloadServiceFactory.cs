using System;
using System.Net.Http;
using System.Net.Http.Headers;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class HttpDownloadServiceFactory : IHttpDownloadServiceFactory
    {
        private readonly OctoLogger _log;
        private readonly IHttpClientFactory _clientFactory;
        private readonly FileSystemProvider _fileSystemProvider;
        private readonly IVersionProvider _versionProvider;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public HttpDownloadServiceFactory(OctoLogger log, IHttpClientFactory clientFactory, FileSystemProvider fileSystemProvider, IVersionProvider versionProvider, EnvironmentVariableProvider environmentVariableProvider)
        {
            _log = log;
            _clientFactory = clientFactory;
            _fileSystemProvider = fileSystemProvider;
            _versionProvider = versionProvider;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public HttpDownloadService Create(string personalAccessToken = null)
        {
            var httpClient = _clientFactory.CreateClient("Default");
            personalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            ConfigureClient(httpClient, personalAccessToken);

            return new HttpDownloadService(_log, httpClient, _fileSystemProvider);
        }

        public HttpDownloadService CreateClientNoSsl(string personalAccessToken = null)
        {
            var httpClient = _clientFactory.CreateClient("NoSSL");
            personalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            ConfigureClient(httpClient, personalAccessToken);

            return new HttpDownloadService(_log, httpClient, _fileSystemProvider);
        }

        private void ConfigureClient(HttpClient httpClient, string personalAccessToken)
        {
            if (httpClient is not null)
            {
                httpClient.Timeout = TimeSpan.FromHours(1);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", _versionProvider?.GetCurrentVersion()));
                if (_versionProvider?.GetVersionComments() is { } comments)
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
                }
            }
        }
    }
}

