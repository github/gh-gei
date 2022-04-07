using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class AdoApiFactory
    {
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

        public virtual AdoApi Create(string personalAccessToken = null)
        {
            personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var adoClient = new AdoClient(_octoLogger, _client, _versionProvider, personalAccessToken);
            return new AdoApi(adoClient);
        }
    }
}
