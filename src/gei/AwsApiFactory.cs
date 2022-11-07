namespace OctoshiftCLI.GithubEnterpriseImporter;

public class AwsApiFactory
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public AwsApiFactory(EnvironmentVariableProvider environmentVariableProvider)
    {
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual AwsApi Create(string awsAccessKey = null, string awsSecretKey = null, string awsSessionToken = null, string awsRegion = null, string awsS3UseSignatureVersion4 = null)
    {
        var accessKey = awsAccessKey ?? _environmentVariableProvider.AwsAccessKey();
        var secretKey = awsSecretKey ?? _environmentVariableProvider.AwsSecretKey();
        var sessionToken = awsSessionToken ?? _environmentVariableProvider.AwsSessionToken();
        if (sessionToken is null) { 
            return new AwsApi(accessKey, secretKey);
        } else {
            return new AwsApi(accessKey, secretKey, sessionToken, awsRegion, awsS3UseSignatureVersion4);
        }
    }
}
