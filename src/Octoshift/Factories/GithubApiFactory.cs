using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public sealed class GithubApiFactory : ISourceGithubApiFactory, ITargetGithubApiFactory
{
    private const string DEFAULT_API_URL = "https://api.github.com";
    private const string DEFAULT_UPLOADS_URL = "https://uploads.github.com";

    private readonly OctoLogger _octoLogger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly DateTimeProvider _dateTimeProvider;
    private readonly RetryPolicy _retryPolicy;
    private readonly IVersionProvider _versionProvider;

    public GithubApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, DateTimeProvider dateTimeProvider, RetryPolicy retryPolicy, IVersionProvider versionProvider)
    {
        _octoLogger = octoLogger;
        _clientFactory = clientFactory;
        _environmentVariableProvider = environmentVariableProvider;
        _dateTimeProvider = dateTimeProvider;
        _retryPolicy = retryPolicy;
        _versionProvider = versionProvider;
    }

    GithubApi ISourceGithubApiFactory.Create(string apiUrl, string uploadsUrl, string sourcePersonalAccessToken)
    {
        apiUrl ??= DEFAULT_API_URL;
        uploadsUrl ??= DEFAULT_UPLOADS_URL;
        sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
        var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), _versionProvider, _retryPolicy, _dateTimeProvider, sourcePersonalAccessToken);
        var multipartUploader = new ArchiveUploader(githubClient, uploadsUrl, _octoLogger, _retryPolicy);
        return new GithubApi(githubClient, apiUrl, _retryPolicy, multipartUploader);
    }

    GithubApi ISourceGithubApiFactory.CreateClientNoSsl(string apiUrl, string uploadsUrl, string sourcePersonalAccessToken)
    {
        apiUrl ??= DEFAULT_API_URL;
        uploadsUrl ??= DEFAULT_UPLOADS_URL;
        sourcePersonalAccessToken ??= _environmentVariableProvider.SourceGithubPersonalAccessToken();
        var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("NoSSL"), _versionProvider, _retryPolicy, _dateTimeProvider, sourcePersonalAccessToken);
        var multipartUploader = new ArchiveUploader(githubClient, uploadsUrl, _octoLogger, _retryPolicy);
        return new GithubApi(githubClient, apiUrl, _retryPolicy, multipartUploader);
    }

    GithubApi ITargetGithubApiFactory.Create(string apiUrl, string uploadsUrl, string targetPersonalAccessToken)
    {
        apiUrl ??= DEFAULT_API_URL;
        uploadsUrl ??= DEFAULT_UPLOADS_URL;
        targetPersonalAccessToken ??= _environmentVariableProvider.TargetGithubPersonalAccessToken();
        var githubClient = new GithubClient(_octoLogger, _clientFactory.CreateClient("Default"), _versionProvider, _retryPolicy, _dateTimeProvider, targetPersonalAccessToken);
        var multipartUploader = new ArchiveUploader(githubClient, uploadsUrl, _octoLogger, _retryPolicy);
        return new GithubApi(githubClient, apiUrl, _retryPolicy, multipartUploader);
    }
}
