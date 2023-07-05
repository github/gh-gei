using OctoshiftCLI.Services;

namespace OctoshiftCLI.BbsToGithub.Factories;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual AwsApi Create(string awsRegion = null, string awsAccessKeyId = null, string awsSecretAccessKey = null, string awsSessionToken = null)
    {
        awsAccessKeyId ??= _environmentVariableProvider.AwsAccessKeyId(false);
        awsSecretAccessKey ??= _environmentVariableProvider.AwsSecretAccessKey(false);
        awsSessionToken ??= _environmentVariableProvider.AwsSessionToken(false);
        awsRegion ??= _environmentVariableProvider.AwsRegion(false);

        return new AwsApi(awsAccessKeyId, awsSecretAccessKey, awsRegion, awsSessionToken);
    }
}
