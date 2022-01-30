using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public GithubApiFactory(OctoLogger octoLogger, EnvironmentVariableProvider environmentVariableProvider)
        {
            _octoLogger = octoLogger;
            _client = new HttpClientFactory().CreateClient("OctoShift");
            _environmentVariableProvider = environmentVariableProvider;
        }

        GithubApi ISourceGithubApiFactory.Create()
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, githubPat);
            return new GithubApi(githubClient);
        }

        GithubApi ITargetGithubApiFactory.Create()
        {
            var githubPat = _environmentVariableProvider.TargetGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, githubPat);
            return new GithubApi(githubClient);
        }
    }
}