using System.Net.Http;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI.BbsToGithub
{
    public class BbsApiFactory
    {
        private readonly OctoLogger _octoLogger;
        private readonly HttpClient _client;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly IVersionProvider _versionProvider;
        private readonly RetryPolicy _retryPolicy;

        public BbsApiFactory(OctoLogger octoLogger, HttpClient client, EnvironmentVariableProvider environmentVariableProvider, IVersionProvider versionProvider, RetryPolicy retryPolicy)
        {
            _octoLogger = octoLogger;
            _client = client;
            _environmentVariableProvider = environmentVariableProvider;
            _versionProvider = versionProvider;
            _retryPolicy = retryPolicy;
        }

        public virtual BbsApi Create(string bbsServerUrl, string bbsUsername, string bbsPassword)
        {
            // TODO: Add these variables to environmentVariableProvider
            // bbsServerUrl ??= _environmentVariableProvider.BbsServerUrl();
            // bbsUsername ??= _environmentVariableProvider.BbsUsername();
            // bbsPassword ??= _environmentVariableProvider.BbsPassword();
            var bbsClient = new BbsClient(_octoLogger, _client, _versionProvider, _retryPolicy, bbsUsername, bbsPassword);
            return new BbsApi(bbsClient, bbsServerUrl, _octoLogger);
        }
    }
}
