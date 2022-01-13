using System;

namespace OctoshiftCLI.gei
{

    public class EnvironmentVariableProvider
    {
        private const string GH_PAT = "GH_PAT";

        private readonly OctoLogger _logger;

        public EnvironmentVariableProvider(OctoLogger logger)
        {
            _logger = logger;
        }

        public virtual string GithubPersonalAccessToken() => GetSecret(GH_PAT);

        private string GetSecret(string secretName)
        {
            var secret = Environment.GetEnvironmentVariable(secretName) ?? throw new ArgumentNullException($"{secretName} environment variable is not set.");

            _logger?.RegisterSecret(secret);

            return secret;
        }
    }
}