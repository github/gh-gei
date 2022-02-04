using System.Net.Http;
using Azure.Storage.Blobs;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class AzureApiFactory : IAzureApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public AzureApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
        }

        AzureApi IAzureApiFactory.Create(string azureStorageConnectionString)
        {
            var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

            var blobServiceClient = new BlobServiceClient(connectionString);
            return new AzureApi(_client, blobServiceClient, connectionString);
        }
    }
}
