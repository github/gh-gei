namespace OctoshiftCLI.BbsToGithub
{
    public sealed class AwsApiFactory : IAwsApiFactory
    {
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private readonly OctoLogger _octoLogger;

        public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider, OctoLogger octoLogger)
        {
            _environmentVariableProvider = environmentVariableProvider;
            _octoLogger = octoLogger;
        }

        public AwsApi Create(string awsAccessKey = null, string awsSecretKey = null)
        {
            var accessKey = string.IsNullOrWhiteSpace(awsAccessKey) ? _environmentVariableProvider.AwsAccessKey() : awsAccessKey;
            var secretKey = string.IsNullOrWhiteSpace(awsSecretKey) ? _environmentVariableProvider.AwsSecretKey() : awsSecretKey;

            return new AwsApi(accessKey, secretKey);
        }
    }
}
