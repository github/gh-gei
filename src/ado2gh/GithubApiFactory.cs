using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class GithubApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly RetryPolicy _retryPolicy;

        public GithubApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, RetryPolicy retryPolicy)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _retryPolicy = retryPolicy;
        }

        public virtual GithubApi Create()
        {
            return Create("https://api.github.com");
        }

        public virtual GithubApi Create(string apiUrl)
        {
            var githubPat = _environmentVariableProvider.GithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, githubPat);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }
    }
}
