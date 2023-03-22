using System;
using System.Collections.Generic;
using System.Net.Http;
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

        protected TestHelper _helper { get; }
        protected Dictionary<string, string> _tokens { get; }
        protected DateTime _startTime { get; }

        protected AdoToGithub(ITestOutputHelper output)
        {
            _startTime = DateTime.Now;
            _output = output;

            var logger = new OctoLogger(x => { }, x => _output.WriteLine(x), x => { }, x => { });

            _versionClient = new HttpClient();
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");
            _adoHttpClient = new HttpClient();
            var retryPolicy = new RetryPolicy(logger);
            var adoClient = new AdoClient(logger, _adoHttpClient, new VersionChecker(_versionClient, logger), retryPolicy, adoToken);
            var adoApi = new AdoApi(adoClient, "https://dev.azure.com", logger);

            var githubToken = Environment.GetEnvironmentVariable("GHEC_PAT");
            _githubHttpClient = new HttpClient();
            var githubClient = new GithubClient(logger, _githubHttpClient, new VersionChecker(_versionClient, logger), new RetryPolicy(logger), new DateTimeProvider(), githubToken);
            var githubApi = new GithubApi(githubClient, "https://api.github.com", new RetryPolicy(logger));

            _tokens = new Dictionary<string, string>
            {
                ["GH_PAT"] = githubToken,
                ["ADO_PAT"] = adoToken
            };

            _helper = new TestHelper(_output, adoApi, githubApi, adoClient, githubClient);
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
