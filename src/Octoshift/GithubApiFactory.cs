using System;

namespace OctoshiftCLI
{
    public class GithubApiFactory : IDisposable
    {
        private GithubApi _api;
        private string _token;
        private readonly OctoLogger _log;
        private bool disposedValue;

        public GithubApiFactory(OctoLogger log) => _log = log;
        public GithubApiFactory(GithubApi api) => _api = api;
        public GithubApiFactory(string token) => _token = token;

        public GithubApi Create()
        {
            if (_api != null)
            {
                return _api;
            }

            var githubToken = GetGithubToken();
            var client = new GithubClient(_log, githubToken);
            _api = new GithubApi(client);

            return _api;
        }

        public virtual string GetGithubToken()
        {
            if (!string.IsNullOrWhiteSpace(_token))
            {
                return _token;
            }

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                _log.LogError("NO GH_PAT FOUND IN ENV VARS, exiting...");
                return null;
            }

            _token = githubToken;
            return _token;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _api?.Dispose();
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