namespace OctoshiftCLI.GithubEnterpriseImporter;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual AwsApi Create(string awsRegion = null, string awsAccessKey = null, string awsSecretKey = null, string awsSessionToken = null)
    {
        awsAccessKey ??= _environmentVariableProvider.AwsAccessKey();
        awsSecretKey ??= _environmentVariableProvider.AwsSecretKey();
        awsSessionToken ??= _environmentVariableProvider.AwsSessionToken(false);
        awsRegion ??= _environmentVariableProvider.AwsRegion(false);

        return new AwsApi(awsAccessKey, awsSecretKey, awsRegion, awsSessionToken);
    }
}
