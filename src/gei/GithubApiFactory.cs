using System.Net.Http;

namespace OctoshiftCLI.GithubEnterpriseImporter;

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

    public virtual GithubApi CreateSourceGithubApi()
    {
        var githubPat = _environmentVariableProvider.SourceGithubPersonalAccessToken();
        var githubClient = new GithubClient(_octoLogger, _client, githubPat);
        return new GithubApi(githubClient);
    }

    public virtual GithubApi CreateTargetGithubClient()
    {
        var githubPat = _environmentVariableProvider.TargetGitHubPersonalAccessToken();
        var githubClient = new GithubClient(_octoLogger, _client, githubPat);
        return new GithubApi(githubClient);
    }
}