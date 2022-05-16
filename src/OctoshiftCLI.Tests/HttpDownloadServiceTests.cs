using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
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
            var command = new HttpDownloadService(httpClient) {
                WriteToFile = (_, contents) =>
                {
                    fileContents = contents;
                    return Task.CompletedTask;
                }
            };

            await command.Download(url, filePath);

            // Assert
            fileContents.Should().Be(expectedFileContents);
        }
    }
}
