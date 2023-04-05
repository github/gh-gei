using System.Net.Http;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub
{
    public class BbsApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly IVersionProvider _versionProvider;
        private readonly RetryPolicy _retryPolicy;

        public BbsApiFactory(OctoLogger octoLogger, IHttpClientFactory clientFactory, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider, RetryPolicy retryPolicy)
        {
            _octoLogger = octoLogger;
            _clientFactory = clientFactory;
            _environmentVariableProvider = environmentVariableProvider;
            _versionProvider = versionProvider;
            _retryPolicy = retryPolicy;
        }

        public virtual BbsApi Create(string bbsServerUrl, string bbsUsername, string bbsPassword, bool noSsl = false)
        {
            bbsUsername ??= _environmentVariableProvider.BbsUsername();
            bbsPassword ??= _environmentVariableProvider.BbsPassword();

            var httpClient = noSsl ? _clientFactory.CreateClient("NoSSL") : _clientFactory.CreateClient("Default");

            var bbsClient = new BbsClient(_octoLogger, httpClient, _versionProvider, _retryPolicy, bbsUsername, bbsPassword);
            return new BbsApi(bbsClient, bbsServerUrl, _octoLogger);
        }

        public virtual BbsApi CreateKerberos(string bbsServerUrl, bool noSsl = false)
        {
            var httpClient = noSsl ? _clientFactory.CreateClient("KerberosNoSSL") : _clientFactory.CreateClient("Kerberos");

            var bbsClient = new BbsClient(_octoLogger, httpClient, _versionProvider, _retryPolicy);
            return new BbsApi(bbsClient, bbsServerUrl, _octoLogger);
        }
    }
}
