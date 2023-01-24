using System;
using System.Collections.Generic;
using Amazon.Runtime;

namespace OctoshiftCLI;

public class AwsCredentialProvider
{
    private readonly AWSArgs _awsArgs;
    public AwsCredentialProvider(AWSArgs awsArgs)
    {
        _awsArgs = awsArgs;
    }

    public AWSCredentials GetCredentials()
    {
        if (string.IsNullOrEmpty(_awsArgs.AwsCredentialType)) //load credentials from provider chain
        {
            var credentialsChain = new List<Func<AWSCredentials>>
            {
                SessionCredentials, BasicCredentials, WebIdentityCredentials
            };
            foreach (var credentials in credentialsChain)
            {
                var result = credentials();
                if (result != null)
                {
                    return result;
                }
            }
            throw new OctoshiftCliException("No AWS credentials could be found");
        }
        else
        { //use specific way to load credentials
            return _awsArgs.AwsCredentialType == "basic"
                ? BasicCredentials()
                : _awsArgs.AwsCredentialType == "session"
                    ? SessionCredentials()
                    : _awsArgs.AwsCredentialType == "webIdentity"
                                    ? WebIdentityCredentials()
                                    : throw new ArgumentException("Unknown AwsCredentialType:" + _awsArgs.AwsCredentialType);
        }
    }

    private SessionAWSCredentials SessionCredentials()
    {
        var awsAccessKey = _awsArgs.AwsAccessKey;
        var awsSecretKey = _awsArgs.AwsSecretKey;
        var awsSessionToken = _awsArgs.AwsSessionToken;
        return string.IsNullOrEmpty(awsAccessKey) || string.IsNullOrEmpty(awsSecretKey) || string.IsNullOrEmpty(awsSessionToken)
            ? null
            : new SessionAWSCredentials(awsAccessKey, awsSecretKey, awsSessionToken);
    }

    private AssumeRoleWithWebIdentityCredentials WebIdentityCredentials()
    {
        return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE"))
            ? null
            : AssumeRoleWithWebIdentityCredentials.FromEnvironmentVariables();
    }

    private BasicAWSCredentials BasicCredentials()
    {
        var awsAccessKey = _awsArgs.AwsAccessKey;
        var awsSecretKey = _awsArgs.AwsSecretKey;
        return string.IsNullOrEmpty(awsAccessKey) || string.IsNullOrEmpty(awsSecretKey)
            ? null
            : new BasicAWSCredentials(awsAccessKey, awsSecretKey);
    }

}
