using System.Net.Http;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.BbsToGithub
{
    public class GithubApiFactory : ITargetGithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";

        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly RetryPolicy _retryPolicy;
        private readonly IVersionProvider _versionProvider;

        public GithubApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, RetryPolicy retryPolicy, IVersionProvider versionProvider)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _retryPolicy = retryPolicy;
            _versionProvider = versionProvider;
        }

        public virtual GithubApi Create(string apiUrl = null, string targetPersonalAccessToken = null)
        {
            apiUrl ??= DEFAULT_API_URL;
            targetPersonalAccessToken ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, _versionProvider, _retryPolicy, targetPersonalAccessToken);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }
    }
}
