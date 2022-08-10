using System.Net.Http;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoApiFactory
    {
        private const string DEFAULT_API_URL = "https://dev.azure.com";

        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly IVersionProvider _versionProvider;
        private readonly RetryPolicy _retryPolicy;

        public AdoApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider, RetryPolicy retryPolicy)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _versionProvider = versionProvider;
            _retryPolicy = retryPolicy;
        }

        public virtual AdoApi Create(string personalAccessToken)
        {
            personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var adoClient = new AdoClient(_octoLogger, _client, _versionProvider, _retryPolicy, personalAccessToken);
            return new AdoApi(adoClient, DEFAULT_API_URL, _octoLogger);
        }
    }
}
