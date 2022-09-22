using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace OctoshiftCLI
{
    public class AwsApi
    {
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;
        private readonly OctoLogger _log;

        private static readonly RegionEndpoint regionEndpoint = RegionEndpoint.USEast1;

        private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 24;

        public AwsApi(string awsAccessKey, string awsSecretKey, OctoLogger log)
        {
            _awsAccessKey = awsAccessKey;
            _awsSecretKey = awsSecretKey;
            _log = log;
        }

        public virtual async Task<Uri> UploadToBucket(string bucketName, string fileName, string keyName)
        {
            using var amazonS3Client = new AmazonS3Client(_awsAccessKey, _awsSecretKey, regionEndpoint);
            using var transferUtility = new TransferUtility(amazonS3Client);

            await transferUtility.UploadAsync(fileName, bucketName, keyName);

            return GetPreSignedUrlForFile(amazonS3Client, bucketName, keyName);
        }

        private Uri GetPreSignedUrlForFile(AmazonS3Client amazonS3Client, string bucketName, string keyName)
        {
            var expires = DateTime.Now.AddHours(AUTHORIZATION_TIMEOUT_IN_HOURS);

            var urlRequest = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = keyName,
                Expires = expires
            };

            var urlString = amazonS3Client.GetPreSignedURL(urlRequest);

            return new Uri(urlString);
        }
    }
}
