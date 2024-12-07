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
    private const int UPLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;

    private readonly ITransferUtility _transferUtility;
    private readonly object _mutex = new();
    private readonly OctoLogger _log;
    private DateTime _nextProgressReport = DateTime.Now;

    public AwsApi(ITransferUtility transferUtility, OctoLogger log)
    {
        _transferUtility = transferUtility;
        _log = log;
    }

#pragma warning disable CA2000
    public AwsApi(OctoLogger log, string awsAccessKeyId, string awsSecretAccessKey, string awsRegion = null, string awsSessionToken = null)
        : this(new TransferUtility(BuildAmazonS3Client(awsAccessKeyId, awsSecretAccessKey, awsRegion, awsSessionToken)), log)
#pragma warning restore CA2000
    {
    }

    private static AmazonS3Client BuildAmazonS3Client(string awsAccessKeyId, string awsSecretAccessKey, string awsRegion, string awsSessionToken)
    {
        var regionEndpoint = awsRegion.IsNullOrWhiteSpace() ? null : GetRegionEndpoint(awsRegion);
        AWSConfigsS3.UseSignatureVersion4 = true;

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
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = bucketName,
                Key = keyName,
                FilePath = fileName
            };
            return await UploadToBucket(uploadRequest);
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException)
        {
            throw new OctoshiftCliException($"Upload of archive \"{fileName}\" to AWS timed out", ex);
        }
    }

    public virtual async Task<string> UploadToBucket(string bucketName, Stream content, string keyName)
    {
        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = bucketName,
            Key = keyName,
            InputStream = content
        };
        return await UploadToBucket(uploadRequest);
    }

    private async Task<string> UploadToBucket(TransferUtilityUploadRequest uploadRequest)
    {
        uploadRequest.UploadProgressEvent += (_, args) => LogProgress(args.PercentDone, args.TransferredBytes, args.TotalBytes);
        await _transferUtility.UploadAsync(uploadRequest);

        return GetPreSignedUrlForFile(uploadRequest.BucketName, uploadRequest.Key);
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

    private void LogProgress(int percentDone, long uploadedBytes, long totalBytes)
    {
        lock (_mutex)
        {
            if (DateTime.Now < _nextProgressReport)
            {
                return;
            }

            _nextProgressReport = _nextProgressReport.AddSeconds(UPLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS);
        }

        var progressMessage = uploadedBytes > 0
            ? $", {uploadedBytes.ToLogFriendlySize()} out of {totalBytes.ToLogFriendlySize()} ({percentDone}%) completed"
            : "";

        _log.LogInformation($"Archive upload in progress{progressMessage}...");
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
