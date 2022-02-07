using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AzureApiTests
    {
        [Fact]
        public async Task DownloadFileTo_Should_Succeed()
        {
            var client = new Mock<HttpClient>();
            var blobServiceClient = new Mock<BlobServiceClient>();
            var connectionString = "connectionString";
            var url = "http://example.com/file.zip";
            var tempPath = Path.GetTempPath();
            var filePath = $"{tempPath}/file.zip";

            var azureApi = new AzureApi(client.Object, blobServiceClient.Object, connectionString);

            await azureApi.DownloadFileTo(url, filePath);

            File.Exists(filePath).Should().BeTrue();
            File.Delete(filePath);
        }

        [Fact]
        public async Task UploadToBlob_Should_Fail_On_Invalid_Credentials()
        {
            var client = new Mock<HttpClient>();
            var blobServiceClient = new Mock<BlobServiceClient>();
            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobClient = new Mock<BlobClient>();
            var connectionString = "connectionString";
            var fileName = "file.zip";
            var tempPath = Path.GetTempPath();
            var filePath = $"{tempPath}/file.zip";

            var azureApi = new AzureApi(client.Object, blobServiceClient.Object, connectionString);

            var response = new Mock<Azure.Response<BlobContainerClient>>();

            var responseUpload = new Mock<Azure.Response<Azure.Storage.Blobs.Models.BlobContentInfo>>();

            blobServiceClient
                .Setup(x => x.CreateBlobContainerAsync(
                    It.Is<string>(x => true),
                    It.IsAny<PublicAccessType>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);

            blobClient.SetupGet(x => x.CanGenerateSasUri).Returns(false);

            response.Setup(x => x.Value).Returns(blobContainerClient.Object);

            blobContainerClient.Setup(x => x.GetBlobClient(It.Is<string>(x => true))).Returns(blobClient.Object);

            blobClient.Setup(x => x.UploadAsync(It.Is<string>(x => true), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(responseUpload.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await azureApi.UploadToBlob(fileName, filePath));
        }
    }
}
