using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;


namespace OctoshiftCLI.Tests.Octoshift.Services;

public class AwsApiTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    [Fact]
    public async Task UploadFileToBucket_Should_Succeed()
    {
        // Arrange
        var bucketName = "bucket";
        var fileName = "file.zip";
        var keyName = "key";
        var url = "http://example.com/file.zip";

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        s3Client.Setup(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>())).Returns(url);
        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);
        using var awsApi = new AwsApi(transferUtility.Object, _mockOctoLogger.Object);

        var result = await awsApi.UploadToBucket(bucketName, fileName, keyName);

        // Assert 
        result.Should().Be(url);
        transferUtility.Verify(m => m.UploadAsync(
            It.Is<TransferUtilityUploadRequest>(req => req.BucketName == bucketName && req.Key == keyName && req.FilePath == fileName),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task UploadToBucket_Uploads_FileStream()
    {
        // Arrange
        var bucketName = "bucket";
        var expectedContent = "here are some bytes";
        using var stream = new MemoryStream();
        stream.Write(expectedContent.ToBytes());
        var keyName = "key";
        var url = "http://example.com/file.zip";

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        s3Client.Setup(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>())).Returns(url);
        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);
        using var awsApi = new AwsApi(transferUtility.Object, _mockOctoLogger.Object);

        var result = await awsApi.UploadToBucket(bucketName, stream, keyName);

        // Assert
        result.Should().Be(url);
        transferUtility.Verify(m => m.UploadAsync(
            It.Is<TransferUtilityUploadRequest>(req =>
                req.BucketName == bucketName && req.Key == keyName && (req.InputStream as MemoryStream).ToArray().GetString() == expectedContent),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void It_Throws_If_Aws_Region_Is_Invalid()
    {
        // Arrange, Act
        const string awsRegion = "invalid-region";
        var awsApi = () => new AwsApi(_mockOctoLogger.Object, "awsAccessKeyId", "awsSecretAccessKey", awsRegion);

        // Assert
        awsApi.Should().Throw<OctoshiftCliException>().WithMessage($"*{awsRegion}*");
    }

    [Fact]
    public async Task UploadFileToBucket_Throws_If_TaskCanceledException_From_Timeout()
    {
        // Arrange
        var bucketName = "bucket";
        var fileName = "file.zip";
        var keyName = "key";

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);

        transferUtility.Setup(m => m.UploadAsync(
            It.Is<TransferUtilityUploadRequest>(req => req.BucketName == bucketName && req.Key == keyName && req.FilePath == fileName),
            It.IsAny<CancellationToken>())).Throws(new TaskCanceledException());

        using var awsApi = new AwsApi(transferUtility.Object, _mockOctoLogger.Object);

        // Assert
        s3Client.Verify(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()), Times.Never);

        var exception = await Assert.ThrowsAsync<OctoshiftCliException>(() => awsApi.UploadToBucket(bucketName, fileName, keyName));
        exception.Message.Should().Be($"Upload of archive \"{fileName}\" to AWS timed out");
    }

    [Fact]
    public async Task UploadFileToBucket_Throws_If_TimeoutException_From_Timeout()
    {
        // Arrange
        var bucketName = "bucket";
        var fileName = "file.zip";
        var keyName = "key";

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);

        transferUtility.Setup(m => m.UploadAsync(
            It.Is<TransferUtilityUploadRequest>(req => req.BucketName == bucketName && req.Key == keyName && req.FilePath == fileName),
            It.IsAny<CancellationToken>())).Throws(new TimeoutException());

        using var awsApi = new AwsApi(transferUtility.Object, _mockOctoLogger.Object);

        // Assert
        s3Client.Verify(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()), Times.Never);

        var exception = await Assert.ThrowsAsync<OctoshiftCliException>(() => awsApi.UploadToBucket(bucketName, fileName, keyName));
        exception.Message.Should().Be($"Upload of archive \"{fileName}\" to AWS timed out");
    }
}
