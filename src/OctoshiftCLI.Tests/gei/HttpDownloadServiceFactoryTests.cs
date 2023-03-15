using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class HttpDownloadServiceFactoryTests
    {
        private const string GH_PAT = "GH_PAT";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        private readonly Mock<IVersionProvider> _mockVersionProvider = new Mock<IVersionProvider>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly HttpDownloadServiceFactory _httpDownloadServiceFactory;

        public HttpDownloadServiceFactoryTests()
        {
            _httpDownloadServiceFactory = new HttpDownloadServiceFactory(_mockOctoLogger.Object, _mockHttpClientFactory.Object, _mockFileSystemProvider.Object, _mockVersionProvider.Object, _mockEnvironmentVariableProvider.Object);
        }

        [Fact]
        public void It_Sets_User_Agent_Header_With_Comments()
        {
            // Arrange
            const string currentVersion = "1.1.1.1";
            const string versionComments = "(COMMENTS)";

            _mockEnvironmentVariableProvider
                .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
                .Returns(GH_PAT);

            using var httpClient = new HttpClient();

            _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns(currentVersion);
            _mockVersionProvider.Setup(m => m.GetVersionComments()).Returns(versionComments);

            _mockHttpClientFactory.Setup(m => m.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Act
            _ = _httpDownloadServiceFactory.Create();

            // Assert
            httpClient.DefaultRequestHeaders.UserAgent.Should().HaveCount(2);
            httpClient.DefaultRequestHeaders.UserAgent.ToString().Should().Be($"OctoshiftCLI/{currentVersion} {versionComments}");
        }

        [Fact]
        public void Creates_HttpDownloadService()
        {
            // Arrange
            _mockEnvironmentVariableProvider
                .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
                .Returns(GH_PAT);

            using var httpClient = new HttpClient();

            _mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            var httpDownloadService = _httpDownloadServiceFactory.Create();

            // Assert
            httpDownloadService.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()));
        }

        [Fact]
        public void Creates_HttpDownloadService_With_NoSSL()
        {
            // Arrange
            _mockEnvironmentVariableProvider
                .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
                .Returns(GH_PAT);

            using var httpClient = new HttpClient();

            _mockHttpClientFactory
                .Setup(x => x.CreateClient("NoSSL"))
                .Returns(httpClient);


            // Act
            var httpDownloadService = _httpDownloadServiceFactory.CreateClientNoSsl();

            // Assert
            httpDownloadService.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()));
        }
    }
}

