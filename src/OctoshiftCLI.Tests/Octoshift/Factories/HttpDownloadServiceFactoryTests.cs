using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class HttpDownloadServiceFactoryTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    private readonly Mock<IVersionProvider> _mockVersionProvider = new Mock<IVersionProvider>();

    private readonly HttpDownloadServiceFactory _httpDownloadServiceFactory;

    public HttpDownloadServiceFactoryTests()
    {
        _httpDownloadServiceFactory = new HttpDownloadServiceFactory(_mockOctoLogger.Object, _mockHttpClientFactory.Object, _mockFileSystemProvider.Object, _mockVersionProvider.Object);
    }

    [Fact]
    public void Creates_Default_HttpDownloadService()
    {
        // Arrange
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var httpDownloadService = _httpDownloadServiceFactory.CreateDefault();

        // Assert
        httpDownloadService.Should().NotBeNull();
    }

    [Fact]
    public void Creates_HttpDownloadService_With_NoSSL()
    {
        // Arrange
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);


        // Act
        var httpDownloadService = _httpDownloadServiceFactory.CreateClientNoSsl();

        // Assert
        httpDownloadService.Should().NotBeNull();
    }

    [Fact]
    public void Creates_HttpDownloadService()
    {
        // Arrange
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient(string.Empty))
            .Returns(httpClient);


        // Act
        var httpDownloadService = _httpDownloadServiceFactory.CreateDefaultWithRedirects();

        // Assert
        httpDownloadService.Should().NotBeNull();
    }
}

