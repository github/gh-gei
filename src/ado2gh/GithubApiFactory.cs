using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class GithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";

        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly RetryPolicy _retryPolicy;
        private readonly VersionChecker _versionChecker;

        public GithubApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, RetryPolicy retryPolicy, VersionChecker versionChecker)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _retryPolicy = retryPolicy;
            _versionChecker = versionChecker;
        }

        public virtual GithubApi Create(string apiUrl = null, string personalAccessToken = null)
        {
            apiUrl ??= DEFAULT_API_URL;
            personalAccessToken ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, _versionChecker, personalAccessToken);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }
    }
}
