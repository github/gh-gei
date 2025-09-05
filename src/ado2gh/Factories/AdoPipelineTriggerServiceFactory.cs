using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Factories;

public class AdoPipelineTriggerServiceFactory
{
    private const string DEFAULT_API_URL = "https://dev.azure.com";

    private readonly OctoLogger _octoLogger;
    private readonly HttpClient _client;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly IVersionProvider _versionProvider;
    private readonly RetryPolicy _retryPolicy;

    public AdoPipelineTriggerServiceFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider, RetryPolicy retryPolicy)
    {
        _octoLogger = octoLogger;
        _client = client;
        _environmentVariableProvider = environmentVariableProvider;
        _versionProvider = versionProvider;
        _retryPolicy = retryPolicy;
    }

    public virtual AdoPipelineTriggerService Create(string adoServerUrl, string personalAccessToken)
    {
        adoServerUrl ??= DEFAULT_API_URL;
        personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
        var adoClient = new AdoClient(_octoLogger, _client, _versionProvider, _retryPolicy, personalAccessToken);
        var adoApi = new AdoApi(adoClient, adoServerUrl, _octoLogger);
        return new AdoPipelineTriggerService(adoApi, _octoLogger, adoServerUrl);
    }

    public virtual AdoPipelineTriggerService Create(string personalAccessToken)
    {
        return Create(null, personalAccessToken);
    }
}
