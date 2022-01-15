using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

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
    public void GithubApiFactory_Should_Create_GithubApi_For_Source_Github_Api()
    {
        // Arrange
        var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(_logger);
        environmentVariableProviderMock
            .Setup(m => m.SourceGithubPersonalAccessToken())
            .Returns(SOURCE_GH_PAT);

        using var httpClient = new HttpClient();

        // Act
        ISourceGithubApiFactory factory = new GithubApiFactory(_logger, httpClient, environmentVariableProviderMock.Object);
        var githubApi = factory.Create();

        // Assert
        githubApi.Should().BeOfType<GithubApi>();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(SOURCE_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");
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

        // Act
        ITargetGithubApiFactory factory = new GithubApiFactory(_logger, httpClient, environmentVariableProviderMock.Object);
        var githubApi = factory.Create();

        // Assert
        githubApi.Should().BeOfType<GithubApi>();
        httpClient.DefaultRequestHeaders.Authorization.Parameter.Should().Be(TARGET_GH_PAT);
        httpClient.DefaultRequestHeaders.Authorization.Scheme.Should().Be("Bearer");
    }
}