using System;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public class EnvironmentVariableProvider
    {
        private const string SOURCE_GH_PAT = "GH_SOURCE_PAT";
        private const string TARGET_GH_PAT = "GH_PAT";
        private const string ADO_PAT = "ADO_PAT";
        private const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
        private const string AWS_ACCESS_KEY = "AWS_ACCESS_KEY";
        private const string AWS_SECRET_KEY = "AWS_SECRET_KEY";
        private const string AWS_SESSION_TOKEN = "AWS_SESSION_TOKEN";

        private readonly OctoLogger _logger;

        public EnvironmentVariableProvider(OctoLogger logger)
        {
            _logger = logger;
        }

        public virtual string SourceGithubPersonalAccessToken(bool throwIfNotFound = true) => GetSecret(SOURCE_GH_PAT) ?? TargetGithubPersonalAccessToken(throwIfNotFound);

        public virtual string TargetGithubPersonalAccessToken(bool throwIfNotFound = true) =>
            GetSecret(TARGET_GH_PAT)
            ?? (throwIfNotFound ? throw new OctoshiftCliException($"{TARGET_GH_PAT} environment variable is not set.") : null);

        public virtual string AdoPersonalAccessToken() => GetSecret(ADO_PAT);

        public virtual string AzureStorageConnectionString() => GetSecret(AZURE_STORAGE_CONNECTION_STRING);

        public virtual string AwsSecretKey(bool throwIfNotFound = true) =>
            GetSecret(AWS_SECRET_KEY)
            ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AWS_SECRET_KEY} environment variable is not set.") : null);

        public virtual string AwsAccessKey(bool throwIfNotFound = true) =>
            GetSecret(AWS_ACCESS_KEY)
            ?? (throwIfNotFound ? throw new OctoshiftCliException($"{AWS_ACCESS_KEY} environment variable is not set.") : null);

        public virtual string AwsSessionToken() =>
            GetSecret(AWS_SESSION_TOKEN);

        private string GetSecret(string secretName)
        {
            var secret = Environment.GetEnvironmentVariable(secretName);

            if (string.IsNullOrEmpty(secret))
            {
                return null;
            }

            _logger?.RegisterSecret(secret);

            return secret;
        }
    }
}
