using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter;

public class FileDownloaderTests
{
    private const string EXPECTED_RESPONSE_CONTENT = "RESPONSE_CONTENT";

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
        var octoLoggerMock = TestHelpers.CreateMock<OctoLogger>();

        var fileDownloader = new FileDownloader(httpClient, octoLoggerMock.Object);

        // Act
        var archiveContent = await fileDownloader.DownloadArchive(url);

        // Assert
        Encoding.UTF8.GetString(archiveContent).Should().Be(EXPECTED_RESPONSE_CONTENT);
        octoLoggerMock.Verify(m => m.LogVerbose($"HTTP GET: {url}"));
        octoLoggerMock.Verify(m => m.LogVerbose("RESPONSE (OK): <truncated>"));
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
        var octoLoggerMock = TestHelpers.CreateMock<OctoLogger>();
        var fileDownloader = new FileDownloader(httpClient, octoLoggerMock.Object);

        // Act, Assert
        await fileDownloader
            .Invoking(api => api.DownloadArchive("https://example.com/resource"))
            .Should()
            .ThrowAsync<HttpRequestException>();

        octoLoggerMock.Verify(m => m.LogVerbose("RESPONSE (InternalServerError): <truncated>"));
    }
}
