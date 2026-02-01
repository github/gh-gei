using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Factories;

public class GithubApiFactoryTests
{
    private const string SOURCE_GH_PAT = "SOURCE_GH_PAT";
    private const string TARGET_GH_PAT = "TARGET_GH_PAT";

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new Mock<IHttpClientFactory>();
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

    private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
    private readonly ITargetGithubApiFactory _targetGithubApiFactory;

    public GithubApiFactoryTests()
    {
        _sourceGithubApiFactory = new GithubApiFactory(_mockOctoLogger.Object, _mockHttpClientFactory.Object, _mockEnvironmentVariableProvider.Object, null, null, null);
        _targetGithubApiFactory = (ITargetGithubApiFactory)_sourceGithubApiFactory;
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_With_NoSSL()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(SOURCE_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.CreateClientNoSsl();

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()));
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(SOURCE_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.Create();

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()));
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_When_Source_Pat_Is_Provided()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(SOURCE_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.Create(sourcePersonalAccessToken: SOURCE_GH_PAT);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api_When_Source_Pat_Is_Provided_With_NoSSL()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(SOURCE_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.CreateClientNoSsl(sourcePersonalAccessToken: SOURCE_GH_PAT);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Target_Github_Api()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(TARGET_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _targetGithubApiFactory.Create();

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()));
    }

    [Fact]
    public void GithubApiFactory_Should_Create_GithubApi_For_Target_Github_Api_When_Target_Pat_Is_Provided()
    {
        // Arrange
        _mockEnvironmentVariableProvider
            .Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()))
            .Returns(TARGET_GH_PAT);

        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: TARGET_GH_PAT);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GithubApiFactory_Should_Use_The_Default_Github_Api_Url_If_Passed_In_As_Null_When_Creating_Source_GithubApi()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
        using var httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.Create(null, SOURCE_GH_PAT);
        await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
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
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
        using var httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _targetGithubApiFactory.Create(null, TARGET_GH_PAT);
        await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
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

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
        using var httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);

        // Act
        var githubApi = _sourceGithubApiFactory.CreateClientNoSsl(null, SOURCE_GH_PAT);
        await githubApi.DeleteRepo("org", "repo"); // call a simple/random API method just for the sake of verifying the base API url

        // Assert
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(msg => msg.RequestUri.AbsoluteUri.StartsWith("https://api.github.com")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void Create_Should_Create_Github_Api_With_Github_Pat_From_Environment_If_Not_Provided()
    {
        // Arrange
        var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(TARGET_GH_PAT);

        using var httpClient = new HttpClient();

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var factory = new GithubApiFactory(null, mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null, null, null);
        var targetFactory = (ITargetGithubApiFactory)factory;
        var result = targetFactory.Create();

        // Assert
        result.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()));
    }

    [Fact]
    public void Create_Should_Create_Github_Api_With_Provided_Github_Pat()
    {
        // Arrange
        var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(TARGET_GH_PAT);

        using var httpClient = new HttpClient();

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var factory = new GithubApiFactory(null, mockHttpClientFactory.Object, environmentVariableProviderMock.Object, null, null, null);
        var targetFactory = (ITargetGithubApiFactory)factory;
        var result = targetFactory.Create(targetPersonalAccessToken: TARGET_GH_PAT);

        // Assert
        result.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");

        environmentVariableProviderMock.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
    }
}
