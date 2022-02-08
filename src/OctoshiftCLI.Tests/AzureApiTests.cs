using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AzureApiTests
    {
        private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";

        [Fact]
        public async Task DownloadArchive_Should_Succeed()
        {
            // Arrange  
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EXPECTED_RESPONSE_CONTENT)
            };
            var url = "http://example.com/file.zip";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var blobServiceClient = new Mock<BlobServiceClient>();

            var azureApi = new AzureApi(httpClient, blobServiceClient.Object);

            // Act
            var archiveContent = await azureApi.DownloadArchive(url);

            // Assert
            Encoding.UTF8.GetString(archiveContent).Should().Be(EXPECTED_RESPONSE_CONTENT);
        }

        [Fact]
        public async Task UploadToBlob_Should_Fail_On_Invalid_Credentials()
        {
            // Arrange
            var client = new Mock<HttpClient>();
            var blobServiceClient = new Mock<BlobServiceClient>();
            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobClient = new Mock<BlobClient>();
            var fileName = "file.zip";
            var content = Encoding.UTF8.GetBytes("Upload content").ToArray();

            var azureApi = new AzureApi(client.Object, blobServiceClient.Object);

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
}
