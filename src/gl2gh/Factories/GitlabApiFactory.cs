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

    public virtual GitlabApi Create(string bbsServerUrl, string bbsUsername, string bbsPassword, bool noSsl = false)
    {
        bbsUsername ??= _environmentVariableProvider.GitlabUsername();
        bbsPassword ??= _environmentVariableProvider.GitlabPassword();

        var httpClient = noSsl ? _clientFactory.CreateClient("NoSSL") : _clientFactory.CreateClient("Default");

        var clientRetryPolicy = (_retryPolicy ?? new RetryPolicy(_octoLogger)).WithServiceName("Bitbucket Server");
        var bbsClient = new GitlabClient(_octoLogger, httpClient, _versionProvider, clientRetryPolicy, bbsUsername, bbsPassword);
        return new GitlabApi(bbsClient, bbsServerUrl, _octoLogger);
    }

    public virtual GitlabApi CreateKerberos(string bbsServerUrl, bool noSsl = false)
    {
        var httpClient = noSsl ? _clientFactory.CreateClient("KerberosNoSSL") : _clientFactory.CreateClient("Kerberos");

        var clientRetryPolicy = (_retryPolicy ?? new RetryPolicy(_octoLogger)).WithServiceName("Bitbucket Server");
        var bbsClient = new GitlabClient(_octoLogger, httpClient, _versionProvider, clientRetryPolicy);
        return new GitlabApi(bbsClient, bbsServerUrl, _octoLogger);
    }
}
