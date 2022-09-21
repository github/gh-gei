namespace OctoshiftCLI.BbsToGithub
{
    public interface IAwsApiFactory
    {
        AwsApi Create(string awsAccessKey, string awsSecretKey);
    }
}
