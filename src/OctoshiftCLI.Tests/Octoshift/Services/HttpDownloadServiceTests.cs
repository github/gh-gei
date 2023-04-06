using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

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

        string actualFileContents = null;
        _mockFileSystemProvider.Setup(x => x.CopySourceToTargetStreamAsync(It.IsAny<Stream>(), It.IsAny<Stream>())).Callback<Stream, Stream>((s, _) =>
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            actualFileContents = Encoding.UTF8.GetString(ms.ToArray());
        });


        // Act
        var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object, null);
        await httpDownloadService.DownloadToFile(url, filePath);

        // Assert
        _mockFileSystemProvider.Verify(m => m.Open(filePath, FileMode.Create), Times.Once);
        _mockFileSystemProvider.Verify(m => m.CopySourceToTargetStreamAsync(It.IsAny<Stream>(), It.IsAny<Stream>()), Times.Once);
        actualFileContents.Should().Be(expectedFileContents);
    }

    [Fact]
    public void It_Sets_User_Agent_Header_With_Comments()
    {
        // Arrange
        const string currentVersion = "1.1.1.1";
        const string versionComments = "(COMMENTS)";

        using var httpClient = new HttpClient();

        var mockVersionProvider = new Mock<IVersionProvider>();
        mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns(currentVersion);
        mockVersionProvider.Setup(m => m.GetVersionComments()).Returns(versionComments);

        // Act
        _ = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object, mockVersionProvider.Object);

        // Assert
        httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(2);
        httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be($"OctoshiftCLI/{currentVersion} {versionComments}");
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

        _mockFileSystemProvider.Setup(x => x.Open(filePath, System.IO.FileMode.Open)).Returns(It.IsAny<FileStream>());

        // Act
        var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object, null);

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

        var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object, null);

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
        var httpDownloadService = new HttpDownloadService(_mockOctoLogger.Object, httpClient, _mockFileSystemProvider.Object, null);

        // Act, Assert
        await httpDownloadService
            .Invoking(api => api.DownloadToBytes("https://example.com/resource"))
            .Should()
            .ThrowAsync<HttpRequestException>();

        _mockOctoLogger.Verify(m => m.LogVerbose("RESPONSE (InternalServerError): <truncated>"));
    }
}
