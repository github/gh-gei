using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Factories;

public class GitlabApiFactory
{
    private readonly OctoLogger _octoLogger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly IVersionProvider _versionProvider;
    private readonly RetryPolicy _retryPolicy;

    public GitlabApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider, RetryPolicy retryPolicy)
    {
        _octoLogger = octoLogger;
        _clientFactory = clientFactory;
        _environmentVariableProvider = environmentVariableProvider;
        _versionProvider = versionProvider;
        _retryPolicy = retryPolicy;
    }

    public virtual GitlabApi Create(string gitlabServerUrl, string gitlabPat, bool noSsl = false)
    {
        gitlabPat ??= _environmentVariableProvider.GitlabPat();

        var httpClient = noSsl ? _clientFactory.CreateClient("NoSSL") : _clientFactory.CreateClient("Default");

        var clientRetryPolicy = (_retryPolicy ?? new RetryPolicy(_octoLogger)).WithServiceName("GitLab");
        var gitlabClient = new GitlabClient(_octoLogger, httpClient, _versionProvider, clientRetryPolicy, gitlabPat);
        return new GitlabApi(gitlabClient, gitlabServerUrl, _octoLogger);
    }

    public virtual GitlabApi CreateKerberos(string gitlabServerUrl, bool noSsl = false)
    {
        var httpClient = noSsl ? _clientFactory.CreateClient("KerberosNoSSL") : _clientFactory.CreateClient("Kerberos");

        var clientRetryPolicy = (_retryPolicy ?? new RetryPolicy(_octoLogger)).WithServiceName("GitLab");
        var gitlabClient = new GitlabClient(_octoLogger, httpClient, _versionProvider, clientRetryPolicy);
        return new GitlabApi(gitlabClient, gitlabServerUrl, _octoLogger);
    }
}
