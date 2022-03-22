using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class GithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";
        
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GithubApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public virtual GithubApi Create(string apiUrl = DEFAULT_API_URL, string personalAccessToken = null)
        {
            personalAccessToken ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, personalAccessToken);
            return new GithubApi(githubClient, apiUrl);
        }
    }
}
