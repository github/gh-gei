using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly RetryPolicy _retryPolicy;

        public GithubApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, RetryPolicy retryPolicy)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _retryPolicy = retryPolicy;
        }

        GithubApi ISourceGithubApiFactory.Create() => (this as ISourceGithubApiFactory).Create("https://api.github.com");

        GithubApi ISourceGithubApiFactory.Create(string apiUrl)
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), githubPat);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }

        GithubApi ISourceGithubApiFactory.CreateClientNoSsl(string apiUrl)
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("NoSSL"), githubPat);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }

        GithubApi ITargetGithubApiFactory.Create() => (this as ITargetGithubApiFactory).Create("https://api.github.com");

        GithubApi ITargetGithubApiFactory.Create(string apiUrl)
        {
            var githubPat = _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), githubPat);
            return new GithubApi(githubClient, apiUrl, _retryPolicy);
        }
    }
}
