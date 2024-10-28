using System;
using System.Collections.Generic;
using System.Net.Http;
using OctoshiftCLI.Services;
using Xunit.Abstractions;

namespace OctoshiftCLI.IntegrationTests
{
    public abstract class AdoToGithub : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly HttpClient _adoHttpClient;
        private readonly HttpClient _githubHttpClient;
        private readonly HttpClient _versionClient;
        private bool disposedValue;

        protected TestHelper Helper { get; }
        protected Dictionary<string, string> Tokens { get; }
        protected DateTime StartTime { get; }

        protected AdoToGithub(ITestOutputHelper output, string adoServerUrl = "https://dev.azure.com", string adoPatEnvVar = "ADO_PAT")
        {
            StartTime = DateTime.Now;
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });

            _versionClient = new HttpClient();
            var adoToken = Environment.GetEnvironmentVariable(adoPatEnvVar);
            _adoHttpClient = new HttpClient();
            var retryPolicy = new RetryPolicy(logger);
            var adoClient = new AdoClient(logger, _adoHttpClient, new VersionChecker(_versionClient, logger), retryPolicy, adoToken);
            var adoApi = new AdoApi(adoClient, adoServerUrl, logger);

            var githubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
            _githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, _githubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), new DateTimeProvider(), githubToken);
            var githubApi = new GithubApi(githubClient, "https://api.github.com", new RetryPolicy(logger), null);

            Tokens = new Dictionary<string, string>
            {
                ["GH_PAT"] = githubToken,
                ["ADO_PAT"] = adoToken
            };

            Helper = new TestHelper(_output, adoApi, githubApi, adoClient, githubClient);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _adoHttpClient.Dispose();
                    _githubHttpClient.Dispose();
                    _versionClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
