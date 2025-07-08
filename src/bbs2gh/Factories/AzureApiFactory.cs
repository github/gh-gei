using System.Net.Http;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Factories;

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
    => AzureApiFactoryHelper.Create(
        _clientFactory,
        _blobServiceClientFactory,
        _environmentVariableProvider,
        _octoLogger,
        azureStorageConnectionString);

    public AzureApi CreateClientNoSsl(string azureStorageConnectionString)
    => AzureApiFactoryHelper.CreateClientNoSsl(
        _clientFactory,
        _blobServiceClientFactory,
        _environmentVariableProvider,
        _octoLogger,
        azureStorageConnectionString);
}
