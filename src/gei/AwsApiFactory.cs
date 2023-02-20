using System;
using System.Net.Http;
using Amazon.Runtime;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public class AwsApiFactory
{
    private const string AWS_ACCESS_KEY = "AWS_ACCESS_KEY";
    private const string AWS_SECRET_KEY = "AWS_SECRET_KEY";

    internal static AWSCredentials ResolveStaticCredentials()
    {
        try
        {
            var crd = FallbackCredentialsFactory.GetCredentials();

            // Ensure that the credentials are actually present by calling crd.GetCredentials()
            crd.GetCredentials();

            return crd;
        }
        catch (Exception e) when
        (e is AmazonServiceException or
         HttpRequestException) // HttpRequestException happens if there's a 400 on Amazon.Util.EC2InstanceMetadata.get_IAMSecurityCredentials()
        {
            Console.WriteLine($"Unable to load default AWS credentials, falling back to the legacy environment variables");
            return ResolveLegacyCredentials();
        }
    }


    /// <summary>
    /// This is needed because the default resolution strategy has different variables names:
    /// there's no mention of `AWS_ACCESS_KEY` in the code, just `AWS_SECRET_KEY`:
    /// public const string LEGACY_ENVIRONMENT_VARIABLE_SECRETKEY = "AWS_SECRET_KEY";
    /// </summary>
    /// <returns>
    /// BasicAWSCredentials
    /// </returns>
    private static AWSCredentials ResolveLegacyCredentials()
    {
        Console.WriteLine($"[DEPRECATED] Trying to use {AWS_ACCESS_KEY} and {AWS_SECRET_KEY} environment variables for authentication");
        var ak = Environment.GetEnvironmentVariable(AWS_ACCESS_KEY);
        var sk = Environment.GetEnvironmentVariable(AWS_SECRET_KEY);

        return string.IsNullOrWhiteSpace(ak) || string.IsNullOrWhiteSpace(sk)
            ? throw new OctoshiftCliException($"Both {AWS_SECRET_KEY} and {AWS_ACCESS_KEY} have to be set.")
            : (AWSCredentials)new BasicAWSCredentials(ak, sk);
    }

    public virtual AwsApi Create(string awsAccessKey = null, string awsSecretKey = null)
    {
        var credentials = awsAccessKey == null
            || awsSecretKey == null
            ? ResolveStaticCredentials()
            : new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        return new AwsApi(credentials);
    }
}
