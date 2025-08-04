using System.Net.Http;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public sealed class AzureApiFactory : IAzureApiFactory
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IBlobServiceClientFactory _blobServiceClientFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly OctoLogger _octoLogger;

    public AzureApiFactory(IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, IBlobServiceClientFactory blobServiceClientFactory, OctoLogger octoLogger)
    {
        _clientFactory = clientFactory;
        _environmentVariableProvider = environmentVariableProvider;
        _blobServiceClientFactory = blobServiceClientFactory;
        _octoLogger = octoLogger;
    }

    public AzureApi Create(string azureStorageConnectionString = null)
    {
        var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

        var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
        return new AzureApi(_clientFactory.CreateClient("Default"), blobServiceClient, _octoLogger);
    }

    public AzureApi CreateClientNoSsl(string azureStorageConnectionString)
    {
        var connectionString = string.IsNullOrWhiteSpace(azureStorageConnectionString) ? _environmentVariableProvider.AzureStorageConnectionString() : azureStorageConnectionString;

        var blobServiceClient = _blobServiceClientFactory.Create(connectionString);
        return new AzureApi(_clientFactory.CreateClient("NoSSL"), blobServiceClient, _octoLogger);
    }
}
