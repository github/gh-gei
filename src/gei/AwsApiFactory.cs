using System;
namespace OctoshiftCLI.GithubEnterpriseImporter;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual AwsApi Create(AWSArgs awsArgs)
    {
        if (awsArgs is null)
        {
            throw new ArgumentNullException(nameof(awsArgs));
        }
        if (string.IsNullOrEmpty(awsArgs.AwsAccessKey))
        {
            awsArgs.AwsAccessKey = _environmentVariableProvider.AwsAccessKey(false);
        }
        if (string.IsNullOrEmpty(awsArgs.AwsSecretKey))
        {
            awsArgs.AwsSecretKey = _environmentVariableProvider.AwsSecretKey(false);
        }
        if (string.IsNullOrEmpty(awsArgs.AwsSessionToken))
        {
            awsArgs.AwsSessionToken = _environmentVariableProvider.AwsSessionToken(false);
        }
        return new AwsApi(awsArgs);
    }

    public virtual AwsApi Create(string awsAccessKey = null, string awsSecretKey = null)
    {
        return new AwsApi(awsAccessKey, awsSecretKey);
    }
}
