using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";

        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly RetryPolicy _retryPolicy;
        private readonly IVersionProvider _versionProvider;

        public GithubApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, RetryPolicy retryPolicy, IVersionProvider versionProvider)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _retryPolicy = retryPolicy;
            _versionProvider = versionProvider;
        }

        GithubApi ISourceGithubApiFactory.Create(string apiUrl, string sourcePersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), _versionProvider, sourcePersonalAccessToken);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }

        GithubApi ISourceGithubApiFactory.CreateClientNoSsl(string apiUrl, string sourcePersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("NoSSL"), _versionProvider, sourcePersonalAccessToken);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }

        GithubApi ITargetGithubApiFactory.Create(string apiUrl, string targetPersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            targetPersonalAccessToken ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), _versionProvider, targetPersonalAccessToken);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }
    }
}
