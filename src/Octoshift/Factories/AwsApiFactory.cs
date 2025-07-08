using OctoshiftCLI.Services;

namespace OctoshiftCLI.Factories;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly OctoLogger _octoLogger;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider, OctoLogger octoLogger)
    {
        _environmentVariableProvider = environmentVariableProvider;
        _octoLogger = octoLogger;
    }

    public virtual AwsApi Create(string awsRegion = null, string awsAccessKeyId = null, string awsSecretAccessKey = null, string awsSessionToken = null)
    {
        awsAccessKeyId ??= _environmentVariableProvider.AwsAccessKeyId();
        awsSecretAccessKey ??= _environmentVariableProvider.AwsSecretAccessKey();
        awsSessionToken ??= _environmentVariableProvider.AwsSessionToken(false);
        awsRegion ??= _environmentVariableProvider.AwsRegion();

        return new AwsApi(_octoLogger, awsAccessKeyId, awsSecretAccessKey, awsRegion, awsSessionToken);
    }
}
