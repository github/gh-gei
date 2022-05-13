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
    public class HttpDownloadService
    {
        [Fact]
        public async Task Downloads_File()
        {
            // Arrange
            var url = "example-url";
            var filePath = "example-file";
            var fileContents = (string)null;
            var expectedFileContents = "expected-file-contents";

            var mockHttpClient = TestHelpers.CreateMock<HttpClient>();
            mockHttpClient.Setup(m => m.GetAsync(url, HttpCompletionOption.ResponseHeadersRead));

            // Act
            var command = new HttpDownloadService(mockHttpClient.Object) {
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
