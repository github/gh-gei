using System.IO;
using System.Text;
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
        using var awsApi = new AwsApi(transferUtility.Object);

        var result = await awsApi.UploadToBucket(bucketName, fileName, keyName);

        // Assert 
        result.Should().Be(url);
        transferUtility.Verify(m => m.UploadAsync(fileName, bucketName, keyName, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task UploadToBucket_Uploads_FileStream()
    {
        // Arrange
        var bucketName = "bucket";
        var bytes = Encoding.ASCII.GetBytes("here are some bytes");
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        var keyName = "key";
        var url = "http://example.com/file.zip";

        var transferUtility = new Mock<ITransferUtility>();
        var s3Client = new Mock<IAmazonS3>();

        s3Client.Setup(m => m.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>())).Returns(url);
        transferUtility.Setup(m => m.S3Client).Returns(s3Client.Object);
        using var awsApi = new AwsApi(transferUtility.Object);

        var result = await awsApi.UploadToBucket(bucketName, stream, keyName);

        // Assert
        result.Should().Be(url);
        transferUtility.Verify(m => m.UploadAsync(It.IsAny<MemoryStream>(), bucketName, keyName, It.IsAny<CancellationToken>()));
    }
}
