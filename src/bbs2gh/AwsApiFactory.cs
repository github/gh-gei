namespace OctoshiftCLI.BbsToGithub;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual AwsApi Create(string awsAccessKey = null, string awsSecretKey = null)
    {
        var accessKey = awsAccessKey ?? _environmentVariableProvider.AwsAccessKey();
        var secretKey = awsSecretKey ?? _environmentVariableProvider.AwsSecretKey();

        return new AwsApi(accessKey, secretKey);
    }
}
