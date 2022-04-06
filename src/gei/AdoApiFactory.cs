using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class AdoApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly VersionChecker _versionChecker;

        public AdoApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, VersionChecker versionChecker)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _versionChecker = versionChecker;
        }

        public virtual AdoApi Create(string personalAccessToken = null)
        {
            personalAccessToken ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var adoClient = new AdoClient(_octoLogger, _client, _versionChecker, personalAccessToken);
            return new AdoApi(adoClient);
        }
    }
}
