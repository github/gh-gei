using System.Linq;
using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.bbs2gh.Factories;

public class GitlabApiFactoryTests
{
    private const string BBS_SERVER_URL = "http://bbs.contoso.com:7990";

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new Mock<IHttpClientFactory>();

    private readonly GitlabApiFactory _gitlabApiFactory;

    public GitlabApiFactoryTests()
    {
        _gitlabApiFactory = new GitlabApiFactory(_mockOctoLogger.Object, _mockHttpClientFactory.Object, _mockEnvironmentVariableProvider.Object, null, null);
    }

    [Fact]
    public void Should_Create_GitlabApi_For_Source_Gitlab_Api_With_Kerberos()
    {
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Kerberos"))
            .Returns(httpClient);

        // Act
        var githubApi = _gitlabApiFactory.CreateKerberos(BBS_SERVER_URL);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Accept.First().MediaType.Should().Be("application/json");
    }

    [Fact]
    public void Should_Create_GitlabApi_For_Source_Gitlab_Api_With_Default()
    {
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("Default"))
            .Returns(httpClient);

        // Act
        var githubApi = _gitlabApiFactory.Create(BBS_SERVER_URL, "user", "pass");

        // Assert
        githubApi.Should().NotBeNull();
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
        var githubApi = _gitlabApiFactory.Create(BBS_SERVER_URL, "user", "pass", true);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Accept.First().MediaType.Should().Be("application/json");
    }

    [Fact]
    public void Should_Create_GitlabApi_With_Kerberos_And_No_Ssl_Verify()
    {
        using var httpClient = new HttpClient();

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("KerberosNoSSL"))
            .Returns(httpClient);

        // Act
        var githubApi = _gitlabApiFactory.CreateKerberos(BBS_SERVER_URL, true);

        // Assert
        githubApi.Should().NotBeNull();
        httpClient.DefaultRequestHeaders.Accept.First().MediaType.Should().Be("application/json");
    }
}
