using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class HttpDownloadServiceTests
    {
        private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

        [Fact]
        public async Task Downloads_File()
        {
            // Arrange
            var url = "https://objects-staging-origin.githubusercontent.com/octoshiftmigrationlogs/github/example-repo.txt";
            var filePath = "example-file";
            var expectedFileContents = "expected-file-contents";

            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedFileContents)
            };

            var mockHttpHandler = new Mock<HttpMessageHandler>();

            mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(x => x.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            using var httpClient = new HttpClient(mockHttpHandler.Object);
            _mockFileSystemProvider.Setup(x => x.Open(filePath, It.IsAny<FileMode>())).Returns(It.IsAny<FileStream>());

            // Act
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object);
            await httpDownloadService.DownloadToFile(url, filePath);

            // Assert
            _mockFileSystemProvider.Verify(m => m.OpenRead(filePath), Times.Once);
            _mockFileSystemProvider.Verify(m => m.CopySourceToTargetStreamAsync(It.IsAny<Stream>(), It.IsAny<Stream>()), Times.Once);
        }

        private static string StreamToString(Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        [Fact]
        public async Task Raises_Exception_When_File_Cannot_Be_Downloaded()
        {
            // Arrange
            var url = "https://objects-staging-origin.githubusercontent.com/octoshiftmigrationlogs/github/example-repo.txt";
            var filePath = "example-file";

            using var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            var mockHttpHandler = new Mock<HttpMessageHandler>();

            mockHttpHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(x => x.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            using var httpClient = new HttpClient(mockHttpHandler.Object);

            var path = System.IO.Path.GetTempPath() + "integration_test";

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var fs = File.Create(path);

            _mockFileSystemProvider.Setup(x => x.Open(filePath, System.IO.FileMode.Open)).Returns(fs);

            // Act
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object);

            // Assert
            await FluentActions
                .Invoking(async () => await httpDownloadService.DownloadToFile(url, filePath))
                .Should().ThrowAsync<HttpRequestException>();
        }
        [Fact]
        public async Task DownloadArchive_Should_Succeed()
        {
            // Arrange  
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
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

            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object);

            // Act
            var archiveContent = await httpDownloadService.DownloadToBytes(url);

            // Assert
            Encoding.UTF8.GetString(archiveContent).Should().Be(EXPECTED_RESPONSE_CONTENT);
            _mockOctoLogger.Verify(m => m.LogVerbose($"HTTP GET: {url}"));
            _mockOctoLogger.Verify(m => m.LogVerbose("RESPONSE (OK): <truncated>"));
        }

        [Fact]
        public async Task DownloadArchive_Should_Throw_HttpRequestException_On_Non_Success_Response()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            using var httpClient = new HttpClient(handlerMock.Object);
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object);

            // Act, Assert
            await httpDownloadService
                .Invoking(api => api.DownloadToBytes("https://example.com/resource"))
                .Should()
                .ThrowAsync<HttpRequestException>();

            _mockOctoLogger.Verify(m => m.LogVerbose("RESPONSE (InternalServerError): <truncated>"));
        }
    }
}
