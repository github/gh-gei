using System.Linq;
using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Factories;

public class GitlabApiFactoryTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();

    private readonly GitlabApiFactory _gitlabApiFactory;

    public GitlabApiFactoryTests()
    {
        _gitlabApiFactory = new GitlabApiFactory(_mockOctoLogger.Object, _mockHttpClientFactory.Object, _mockEnvironmentVariableProvider.Object, null, null, null);
    }

    [Fact]
    public void Should_Create_GitlabApi_With_Default()
    {
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var gitlabApi = _gitlabApiFactory.Create(GITLAB_SERVER_URL, "pat");

        // Assert
        gitlabApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Accept.First().MediaType.Should().Be("application/json");
    }

    [Fact]
    public void Should_Create_GitlabApi_With_No_Ssl_Verify()
    {
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("NoSSL"))
            .Returns(httpClient);

        // Act
        var gitlabApi = _gitlabApiFactory.Create(GITLAB_SERVER_URL, "pat", true);

        // Assert
        gitlabApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Accept.First().MediaType.Should().Be("application/json");
    }
}
