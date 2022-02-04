using System;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class EnvironmentVariableProvider
    {
        private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
        private const string TARGET_GH_PAT = "GH_PAT";
        private const string ADO_PAT = "ADO_PAT";
        private readonly OctoLogger _logger;

        public EnvironmentVariableProvider(OctoLogger logger)
        {
            _logger = logger;
        }

        public virtual string SourceGithubPersonalAccessToken() => GetSecret(SOURCE_GH_PAT) ?? TargetGithubPersonalAccessToken();

        public virtual string TargetGithubPersonalAccessToken() =>
            GetSecret(TARGET_GH_PAT) ??
            throw new OctoshiftCliException($"{TARGET_GH_PAT} environment variable is not set.");

        public virtual string AdoPersonalAccessToken() => GetSecret(ADO_PAT);

        private string GetSecret(string secretName)
        {
            var secret = Environment.GetEnvironmentVariable(secretName);

            if (secret is null)
            {
                return null;
            }

            _logger?.RegisterSecret(secret);

            return secret;
        }
    }
}