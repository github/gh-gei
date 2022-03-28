using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
    {
        private const string DEFAULT_API_URL = "https://api.github.com";

        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GithubApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
        }

        GithubApi ISourceGithubApiFactory.Create(string apiUrl, string sourcePersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), sourcePersonalAccessToken);
            return new GithubApi(githubClient, apiUrl);
        }

        GithubApi ISourceGithubApiFactory.CreateClientNoSsl(string apiUrl, string sourcePersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("NoSSL"), sourcePersonalAccessToken);
            return new GithubApi(githubClient, apiUrl);
        }

        GithubApi ITargetGithubApiFactory.Create(string apiUrl, string targetPersonalAccessToken)
        {
            apiUrl ??= DEFAULT_API_URL;
            targetPersonalAccessToken ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), targetPersonalAccessToken);
            return new GithubApi(githubClient, apiUrl);
        }
    }
}
