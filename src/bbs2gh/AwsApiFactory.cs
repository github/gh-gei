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
        var accessKey = _environmentVariableProvider.AwsAccessKey() ?? awsAccessKey;
        var secretKey = _environmentVariableProvider.AwsSecretKey() ?? awsSecretKey;

        return new AwsApi(accessKey, secretKey);
    }
}
