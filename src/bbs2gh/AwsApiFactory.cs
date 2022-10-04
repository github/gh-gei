namespace OctoshiftCLI.BbsToGithub;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly OctoLogger _octoLogger;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider, OctoLogger octoLogger)
    {
        _environmentVariableProvider = environmentVariableProvider;
        _octoLogger = octoLogger;
    }

    public virtual AwsApi Create(string awsAccessKey = null, string awsSecretKey = null)
    {
        var accessKey = _environmentVariableProvider.AwsAccessKey() ?? awsAccessKey;
        var secretKey = _environmentVariableProvider.AwsSecretKey() ?? awsSecretKey;

        return new AwsApi(accessKey, secretKey);
    }
}
