using System;

namespace OctoshiftCLI.GithubEnterpriseImporter
{

    public class EnvironmentVariableProvider
    {
        private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
        private const string TARGET_GH_PAT = "GH_PAT";
        private readonly OctoLogger _logger;

        public EnvironmentVariableProvider(OctoLogger logger)
        {
            _logger = logger;
        }

        public virtual string SourceGithubPersonalAccessToken() => GetSecret(SOURCE_GH_PAT);

        public virtual string TargetGitHubPersonalAccessToken() => GetSecret(TARGET_GH_PAT);

        private string GetSecret(string secretName)
        {
            var secret = Environment.GetEnvironmentVariable(secretName) ??
                         throw new ArgumentNullException($"{secretName} environment variable is not set.");

            _logger?.RegisterSecret(secret);

            return secret;
        }
    }
}