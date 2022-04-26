using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class AzureApiFactory : IAzureApiFactory
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IBlobServiceClientFactory _blobServiceClientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public AzureApiFactory(IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, IBlobServiceClientFactory blobServiceClientFactory)
        {
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _blobServiceClientFactory = blobServiceClientFactory;
        }

        public AzureApi Create(string azureStorageConnectionString = null)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

            var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
            return new AzureApi(_clientFactory.CreateClient("Default"), blobServiceClient);
        }

        public AzureApi CreateClientNoSsl(string azureStorageConnectionString)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

            var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
            return new AzureApi(_clientFactory.CreateClient("NoSSL"), blobServiceClient);
        }
    }
}
