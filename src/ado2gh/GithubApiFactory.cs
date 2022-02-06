using System.Net.Http;

namespace OctoshiftCLI.AdoToGithub
{
    public class GithubApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GithubApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
        }

        public virtual GithubApi Create()
        {
            var githubPat = _environmentVariableProvider.GithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, githubPat, "https://api.github.com");
            return new GithubApi(githubClient);
        }
    }
}
