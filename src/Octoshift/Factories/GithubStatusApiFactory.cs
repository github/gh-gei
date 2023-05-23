using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public class GithubStatusApiFactory
{
    private const string GITHUB_STATUS_API_URL = "https://www.githubstatus.com/api/v2";

    private readonly OctoLogger _octoLogger;
    private readonly HttpClient _client;
    private readonly IVersionProvider _versionProvider;

    public GithubStatusApiFactory(OctoLogger octoLogger, HttpClient client, IVersionProvider versionProvider)
    {
        _octoLogger = octoLogger;
        _client = client;
        _versionProvider = versionProvider;
    }

    public virtual GithubStatusApi Create()
    {
        var githubStatusClient = new GithubStatusClient(_octoLogger, _client, _versionProvider);
        return new GithubStatusApi(githubStatusClient, GITHUB_STATUS_API_URL);
    }
}
