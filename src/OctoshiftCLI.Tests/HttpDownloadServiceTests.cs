using System.Net;
using System.Net.Http;
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
            var httpDownloadService = new HttpDownloadService(httpClient)
            {
                WriteToFile = (_, contents) =>
                {
                    fileContents = contents;
                    return Task.CompletedTask;
                }
            };

            await httpDownloadService.Download(url, filePath);

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
            var httpDownloadService = new HttpDownloadService(httpClient)
            {
                WriteToFile = (_, contents) =>
                {
                    fileContents = contents;
                    return Task.CompletedTask;
                }
            };

            // Assert
            await FluentActions
                .Invoking(async () => await httpDownloadService.Download(url, filePath))
                .Should().ThrowAsync<HttpRequestException>();
        }
    }
}
