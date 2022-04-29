using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoApiFactory
    {
        private const string DEFAULT_API_URL = "https://dev.azure.com";

        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly IVersionProvider _versionProvider;

        public AdoApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _versionProvider = versionProvider;
        }

        public virtual AdoApi Create(string personalAccessToken, string commandName)
        {
            personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var adoClient = new AdoClient(_octoLogger, _client, personalAccessToken, _versionProvider?.GetProductVersionHeaderValue(commandName));
            return new AdoApi(adoClient, DEFAULT_API_URL);
        }
    }
}
