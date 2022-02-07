using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class AzureApiFactory : IAzureApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IBlobServiceClientFactory _blobServiceClientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public AzureApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, IBlobServiceClientFactory blobServiceClientFactory)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _blobServiceClientFactory = blobServiceClientFactory;
        }

        AzureApi IAzureApiFactory.Create(string azureStorageConnectionString)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

            var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
            return new AzureApi(_clientFactory.CreateClient("Default"), blobServiceClient, connectionString);
        }

        AzureApi IAzureApiFactory.CreateClientNoSSL(string azureStorageConnectionString)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

            var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
            return new AzureApi(_clientFactory.CreateClient("NoSSL"), blobServiceClient, connectionString);
        }

    }
}
