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

        private const int AUTHORIZATION_TIMEOUT_IN_HOURS = 24;

        public AwsApi(string awsAccessKey, string awsSecretKey, OctoLogger log)
        {
            _awsAccessKey = awsAccessKey;
            _awsSecretKey = awsSecretKey;
            _log = log;
        }

        public virtual async Task<Uri> UploadToBucket(string bucketName, string fileName, string keyName)
        {
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(_awsAccessKey, _awsSecretKey);
            var bucketRegion = await GetBucketRegion(bucketName);
            using var amazonS3Client = new AmazonS3Client(awsCredentials, bucketRegion);
            using var transferUtility = new TransferUtility(amazonS3Client);

            await transferUtility.UploadAsync(fileName, bucketName, keyName);

            return GetPreSignedUrlForFile(amazonS3Client, bucketName, keyName);
        }

        public virtual async Task<RegionEndpoint> GetBucketRegion(string bucketName)
        {
            // FIXME GetBucketLocation is inaccessible due to its protection level?
            // using var amazonS3Client = new AmazonS3Client(_awsAccessKey, _awsSecretKey);
            // return await amazonS3Client.GetBucketLocation(bucketName);
            //
            // FIXME detect region from bucket name
            return await Task.Run(() => RegionEndpoint.USEast1);
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
