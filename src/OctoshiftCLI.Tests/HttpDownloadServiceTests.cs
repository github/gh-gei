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

        [Fact]
        public async Task Downloads_File()
        {
            // Arrange
            var url = "https://objects-staging-origin.githubusercontent.com/octoshiftmigrationlogs/github/example-repo.txt";
            var filePath = "example-file";
            var fileContents = (string)null;
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

            // Act
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient)
            {
                WriteToFile = (_, contents) =>
                {
                    fileContents = contents;
                    return Task.CompletedTask;
                }
            };

            await httpDownloadService.DownloadToFile(url, filePath);

            // Assert
            fileContents.Should().Be(expectedFileContents);
        }

        [Fact]
        public async Task Raises_Exception_When_File_Cannot_Be_Downloaded()
        {
            // Arrange
            var url = "https://objects-staging-origin.githubusercontent.com/octoshiftmigrationlogs/github/example-repo.txt";
            var filePath = "example-file";
            var fileContents = (string)null;

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

            // Act
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient)
            {
                WriteToFile = (_, contents) =>
                {
                    fileContents = contents;
                    return Task.CompletedTask;
                }
            };

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

            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient);

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
            var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient);

            // Act, Assert
            await httpDownloadService
                .Invoking(api => api.DownloadToBytes("https://example.com/resource"))
                .Should()
                .ThrowAsync<HttpRequestException>();

            _mockOctoLogger.Verify(m => m.LogVerbose("RESPONSE (InternalServerError): <truncated>"));
        }
    }
}
