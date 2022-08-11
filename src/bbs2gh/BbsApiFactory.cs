using System;
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

        public virtual BbsApi Create(string personalAccessToken)
        {
            throw new NotImplementedException();
        }
    }
}
