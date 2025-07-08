using System.Net.Http;
using OctoshiftCLI.Services;
using OctoshiftCLI.Factories;

namespace OctoshiftCLI.GithubEnterpriseImporter.Factories
{
    public sealed class AzureApiFactory : IAzureApiFactory
    {
        private readonly OctoshiftCLI.Factories.AzureApiFactory _factory;

        public AzureApiFactory(
            IHttpClientFactory clientFactory,
            EnvironmentVariableProvider environmentVariableProvider,
            IBlobServiceClientFactory blobServiceClientFactory,
            OctoLogger octoLogger)
        {
            _factory = new OctoshiftCLI.Factories.AzureApiFactory(clientFactory, environmentVariableProvider, blobServiceClientFactory, octoLogger);
        }

        public AzureApi Create(string azureStorageConnectionString = null) 
            => _factory.Create(azureStorageConnectionString);

        public AzureApi CreateClientNoSsl(string azureStorageConnectionString) 
            => _factory.CreateClientNoSsl(azureStorageConnectionString);
    }
}
