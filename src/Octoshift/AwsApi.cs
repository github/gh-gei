using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI;

public class AwsApi : IDisposable
{
    private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 48;
    private static readonly RegionEndpoint DefaultRegionEndpoint = RegionEndpoint.USEast1;

    private readonly ITransferUtility _transferUtility;

#pragma warning disable CA2000
    public AwsApi(string awsAccessKey, string awsSecretKey, string awsRegion = null, string awsSessionToken = null)
        : this(new TransferUtility(BuildAmazonS3Client(awsAccessKey, awsSecretKey, awsRegion, awsSessionToken)))
#pragma warning restore CA2000
    {
    }

    internal AwsApi(ITransferUtility transferUtility) => _transferUtility = transferUtility;

    private static AmazonS3Client BuildAmazonS3Client(string awsAccessKey, string awsSecretKey, string awsRegion, string awsSessionToken)
    {
        var regionEndpoint = DefaultRegionEndpoint;
        if (awsRegion.HasValue())
        {
            regionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);
            AWSConfigsS3.UseSignatureVersion4 = true;
        }

        return awsSessionToken.HasValue()
            ? new AmazonS3Client(awsAccessKey, awsSecretKey, awsSessionToken, regionEndpoint)
            : new AmazonS3Client(awsAccessKey, awsSecretKey, regionEndpoint);
    }

    public virtual async Task<string> UploadToBucket(string bucketName, string fileName, string keyName)
    {
        await _transferUtility.UploadAsync(fileName, bucketName, keyName);
        return GetPreSignedUrlForFile(bucketName, keyName);
    }

    public virtual async Task<string> UploadToBucket(string bucketName, Stream content, string keyName)
    {
        await _transferUtility.UploadAsync(content, bucketName, keyName);
        content.Dispose();
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
