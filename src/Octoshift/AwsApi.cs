using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace OctoshiftCLI;

public class AwsApi : IDisposable
{
    private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 24;
    private static readonly RegionEndpoint RegionEndpoint = RegionEndpoint.USEast1;

    private readonly ITransferUtility _transferUtility;

#pragma warning disable CA2000
    public AwsApi(string awsAccessKey, string awsSecretKey) : this(new TransferUtility(new AmazonS3Client(awsAccessKey, awsSecretKey, RegionEndpoint)))
#pragma warning restore CA2000
    {
    }

    public AwsApi(string awsAccessKey, string awsSecretKey, string awsSessionToken, string awsRegionEndpoint, bool awsS3UseSignatureVersion4)
    {
        var region = string.IsNullOrEmpty(awsRegionEndpoint) ? RegionEndpoint : RegionEndpoint.GetBySystemName(awsRegionEndpoint);
        //use default region
#pragma warning disable CA2000
        _transferUtility = string.IsNullOrEmpty(awsSessionToken)
            ? new TransferUtility(new AmazonS3Client(awsAccessKey, awsSecretKey, region))
            : (ITransferUtility)new TransferUtility(new AmazonS3Client(awsAccessKey, awsSecretKey, awsSessionToken, region));
#pragma warning restore CA2000
        if (awsS3UseSignatureVersion4 == true)
        {
            AWSConfigsS3.UseSignatureVersion4 = true;
        }
    }

    public AwsApi(string awsRegionEndpoint, bool awsS3UseSignatureVersion4)
    {
        var credential = AssumeRoleWithWebIdentityCredentials.FromEnvironmentVariables();
        var region = string.IsNullOrEmpty(awsRegionEndpoint) ? RegionEndpoint : RegionEndpoint.GetBySystemName(awsRegionEndpoint);
        //use default region
#pragma warning disable CA2000
        _transferUtility = new TransferUtility(new AmazonS3Client(credential, region));
#pragma warning restore CA2000
        if (awsS3UseSignatureVersion4 == true)
        {
            AWSConfigsS3.UseSignatureVersion4 = true;
        }
    }

    internal AwsApi(ITransferUtility transferUtility) => _transferUtility = transferUtility;

    public virtual async Task<string> UploadToBucket(string bucketName, string fileName, string keyName)
    {
        await _transferUtility.UploadAsync(fileName, bucketName, keyName);
        return GetPreSignedUrlForFile(bucketName, keyName);
    }

    public virtual async Task<string> UploadToBucket(string bucketName, byte[] bytes, string keyName)
    {
        using var byteStream = new MemoryStream(bytes);
        await _transferUtility.UploadAsync(byteStream, bucketName, keyName);
        return GetPreSignedUrlForFile(bucketName, keyName);
    }

    private string GetPreSignedUrlForFile(string bucketName, string keyName)
    {
        var expires = DateTime.Now.AddHours(AUTHORIZATION_TIMEOUT_IN_HOURS);

        var urlRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = keyName,
            Expires = expires
        };

        return _transferUtility.S3Client.GetPreSignedURL(urlRequest);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transferUtility?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
