using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FluentAssertions;
using Moq;
using Xunit;


namespace OctoshiftCLI.Tests;

public class AwsApiTests
{
    [Fact]
    public async Task UploadToBucket_Should_Succeed()
    {
        // Arrange
        var bucketName = "bucket";
        var fileName = "file.zip";
        var keyName = "key";
        var url = "http://example.com/file.zip";
        var oneDayFromNow = DateTime.Now.AddHours(24);

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        s3Client.Setup(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>())).Returns(url);
        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);
        var awsApi = new AwsApi(transferUtility.Object);

        // Act
        var result = await awsApi.UploadToBucket(bucketName, fileName, keyName);

        awsApi.Dispose();

        // Assert
        result.Should().Be(url);
        transferUtility.Verify(m => m.UploadAsync(fileName, bucketName, keyName, It.IsAny<CancellationToken>()));
    }
}
