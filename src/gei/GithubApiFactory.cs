using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GithubApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
        }

        GithubApi ISourceGithubApiFactory.Create(string baseUrl)
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), githubPat, baseUrl);
            return new GithubApi(githubClient);
        }

        GithubApi ISourceGithubApiFactory.CreateClientNoSSL(string baseUrl)
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("NoSSL"), githubPat, baseUrl);
            return new GithubApi(githubClient);
        }

        GithubApi ITargetGithubApiFactory.Create(string baseUrl)
        {
            var githubPat = _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), githubPat, baseUrl);
            return new GithubApi(githubClient);
        }
    }
}