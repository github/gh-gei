using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class AdoApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public AdoApiFactory() : this(null, null, null) { }

        public AdoApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public virtual AdoApi Create(string personalAccessToken = null)
        {
            personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var adoClient = new AdoClient(_octoLogger, _client, personalAccessToken);
            return new AdoApi(adoClient);
        }
    }
}
