using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
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

        GithubApi ISourceGithubApiFactory.Create()
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
            var githubClient = new GithubClient(_octoLogger, _client, githubPat);
            return new GithubApi(githubClient);
        }

        GithubApi ISourceGithubApiFactory.CreateClientNoSSL()
        {
            var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();

#pragma warning disable CA2000 // We don't want to dispose the handler until the client is disposed
            var client = new HttpClientFactory().CreateClient("Octoshift");
            var githubClient = new GithubClient(_octoLogger, client, githubPat);
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