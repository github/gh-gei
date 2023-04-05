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
#pragma warning disable CS0618
        awsAccessKeyId ??= _environmentVariableProvider.AwsAccessKeyId(false)
                           ?? _environmentVariableProvider.AwsAccessKey(false)
                           ?? _environmentVariableProvider.AwsAccessKeyId();
        awsSecretAccessKey ??= _environmentVariableProvider.AwsSecretAccessKey(false)
                               ?? _environmentVariableProvider.AwsSecretKey(false)
                               ?? _environmentVariableProvider.AwsSecretAccessKey();
#pragma warning restore CS0618
        awsSessionToken ??= _environmentVariableProvider.AwsSessionToken(false);
        awsRegion ??= _environmentVariableProvider.AwsRegion(false);

        return new AwsApi(awsAccessKeyId, awsSecretAccessKey, awsRegion, awsSessionToken);
    }
}
