using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
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
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
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
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
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
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
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
                new GithubApiFactory(_logger, mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
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
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
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
                new GithubApiFactory(_logger, _mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null);
            var githubApi = factory.Create(targetPersonalAccessToken: TARGET_GH_PAT);

            // Assert
            githubApi.Should().NotBeNull();
            httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
            httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

            environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public async Task GithubApiFactory_Should_Use_The_Default_Github_Api_Url_If_Passed_In_As_Null_When_Creating_Source_GithubApi()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
            using var httpClient = new HttpClient(handlerMock.Object);

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory = new GithubApiFactory(_logger, mockHttpClientFactory.Object, null, null);
            var githubApi = factory.Create(null, SOURCE_GH_PAT);
            await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri.StartsWith("https://api.github.com")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GithubApiFactory_Should_Use_The_Default_Github_Api_Url_If_Passed_In_As_Null_When_Creating_Target_GithubApi()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
            using var httpClient = new HttpClient(handlerMock.Object);

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("Default"))
                .Returns(httpClient);

            // Act
            ITargetGithubApiFactory factory = new GithubApiFactory(_logger, mockHttpClientFactory.Object, null, null);
            var githubApi = factory.Create(null, TARGET_GH_PAT);
            await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri.StartsWith("https://api.github.com")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GithubApiFactory_Should_Use_The_Default_Github_Api_Url_If_Passed_In_As_Null_When_Creating_Source_GithubApi_No_Ssl()
        {
            // Arrange
            using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
            using var httpClient = new HttpClient(handlerMock.Object);

            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(x => x.CreateClient("NoSSL"))
                .Returns(httpClient);

            // Act
            ISourceGithubApiFactory factory = new GithubApiFactory(_logger, mockHttpClientFactory.Object, null, null);
            var githubApi = factory.CreateClientNoSsl(null, SOURCE_GH_PAT);
            await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri.StartsWith("https://api.github.com")),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}
