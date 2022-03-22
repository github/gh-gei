using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GithubApiFactoryTests
    {
        private const string SOURCE_GH_PAT = "SOURCE_GH_PAT";
        private const string TARGET_GH_PAT = "TARGET_GH_PAT";

        private readonly OctoLogger _logger;

        public GithubApiFactoryTests()
        {
            _logger = new Mock<OctoLogger>().Object;
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_With_NoSSL()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.SourceGithubPersonalAccessToken())
                .Returns(SOURCE_GH_PAT);

            using var httpClient = new HttpClient();

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("NoSSL"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory =
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.CreateClientNoSsl();

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.SourceGithubPersonalAccessToken());
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.SourceGithubPersonalAccessToken())
                .Returns(SOURCE_GH_PAT);

            using var httpClient = new HttpClient();

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory =
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.Create();

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.SourceGithubPersonalAccessToken());
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_When_Source_Pat_Is_Provided()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.SourceGithubPersonalAccessToken())
                .Returns(SOURCE_GH_PAT);

            using var httpClient = new HttpClient();

            var _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory =
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.Create(sourcePersonalAccessToken: SOURCE_GH_PAT);

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.SourceGithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_When_Source_Pat_Is_Provided_With_NoSSL()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.SourceGithubPersonalAccessToken())
                .Returns(SOURCE_GH_PAT);

            using var httpClient = new HttpClient();

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("NoSSL"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory =
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.CreateClientNoSsl(sourcePersonalAccessToken: SOURCE_GH_PAT);

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.SourceGithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Target_Github_Api()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.TargetGithubPersonalAccessToken())
                .Returns(TARGET_GH_PAT);

            using var httpClient = new HttpClient();

            var _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ITargetGithubApiFactory factory =
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.Create();

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken());
        }

        [Fact]
        public void GithubApiFactory_Should_Create_GithubApi_For_Target_Github_Api_When_Target_Pat_Is_Provided()
        {
            // Arrange
            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
            environmentVariableProviderMock
                .Setup(m => m.TargetGithubPersonalAccessToken())
                .Returns(TARGET_GH_PAT);

            using var httpClient = new HttpClient();

            var _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ITargetGithubApiFactory factory =
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object);
            var githubApi = factory.Create(targetPersonalAccessToken: TARGET_GH_PAT);

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(), Times.Never);
        }
    }
}
