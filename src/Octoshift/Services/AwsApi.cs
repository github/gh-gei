using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class AwsApi : IDisposable
{
    private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 48;
    private static readonly RegionEndpoint DefaultRegionEndpoint = RegionEndpoint.USEast1;

    private readonly ITransferUtility _transferUtility;

#pragma warning disable CA2000
    public AwsApi(string awsAccessKeyId, string awsSecretAccessKey, string awsRegion = null, string awsSessionToken = null)
        : this(new TransferUtility(BuildAmazonS3Client(awsAccessKeyId, awsSecretAccessKey, awsRegion, awsSessionToken)))
#pragma warning restore CA2000
    {
    }

    internal AwsApi(ITransferUtility transferUtility) => _transferUtility = transferUtility;

    private static AmazonS3Client BuildAmazonS3Client(string awsAccessKeyId, string awsSecretAccessKey, string awsRegion, string awsSessionToken)
    {
        var regionEndpoint = DefaultRegionEndpoint;
        if (awsRegion.HasValue())
        {
            regionEndpoint = GetRegionEndpoint(awsRegion);
            AWSConfigsS3.UseSignatureVersion4 = true;
        }

        var creds = awsSessionToken.HasValue()
            ? (AWSCredentials)new SessionAWSCredentials(awsAccessKeyId, awsSecretAccessKey, awsSessionToken)
            : new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);

        var config = new AmazonS3Config
        {
            RegionEndpoint = regionEndpoint,
            Timeout = TimeSpan.FromMinutes(5)
        };

        return new AmazonS3Client(creds, config);
    }

    private static RegionEndpoint GetRegionEndpoint(string awsRegion) => RegionEndpoint.GetBySystemName(awsRegion) is { DisplayName: not "Unknown" } regionEndpoint
        ? regionEndpoint
        : throw new OctoshiftCliException($"Invalid AWS region \"{awsRegion}\".");

    public virtual async Task<string> UploadToBucket(string bucketName, string fileName, string keyName)
    {
        try
        {
            await _transferUtility.UploadAsync(fileName, bucketName, keyName);
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException)
        {
            throw new OctoshiftCliException($"Upload of archive \"{fileName}\" to AWS timed out", ex);
        }

        return GetPreSignedUrlForFile(bucketName, keyName);
    }

    public virtual async Task<string> UploadToBucket(string bucketName, Stream content, string keyName)
    {
        await _transferUtility.UploadAsync(content, bucketName, keyName);
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
