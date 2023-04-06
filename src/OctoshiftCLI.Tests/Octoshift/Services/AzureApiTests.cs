using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class AzureApiTests
{
    [Fact]
    public async Task UploadToBlob_Should_Fail_On_Invalid_Credentials()
    {
        // Arrange
        var client = new Mock<HttpClient>();
        var blobServiceClient = new Mock<BlobServiceClient>();
        var blobContainerClient = new Mock<BlobContainerClient>();
        var blobClient = new Mock<BlobClient>();
        var fileName = "file.zip";
        var bytes = Encoding.UTF8.GetBytes("Upload content").ToArray();
        using var content = new MemoryStream();
        content.Write(bytes, 0, bytes.Length);

        var azureApi = new AzureApi(client.Object, blobServiceClient.Object, TestHelpers.CreateMock<OctoLogger>().Object);

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

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await azureApi.UploadToBlob(fileName, content));
    }
}
