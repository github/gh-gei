using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Services;

public class GhesVersionCheckerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();

    private const string GHES_API_URL = "https://github.contoso.com/api/v3";

    private readonly GhesVersionChecker _service;

    public GhesVersionCheckerTests()
    {
        _service = new GhesVersionChecker(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Older_GHES_Version_Returns_True()
    {
        _mockGithubApi.Setup(m => m.GetEnterpriseServerVersion()).ReturnsAsync("3.7.1");
        var result = await _service.AreBlobCredentialsRequired(GHES_API_URL);
        result.Should().Be(true);
    }

    [Fact]
    public async Task Newer_GHES_Version_Returns_False()
    {
        _mockGithubApi.Setup(m => m.GetEnterpriseServerVersion()).ReturnsAsync("3.8.0");
        var result = await _service.AreBlobCredentialsRequired(GHES_API_URL);
        result.Should().Be(false);
    }

    [Fact]
    public async Task Unrecognized_Version_Returns_True()
    {
        _mockGithubApi.Setup(m => m.GetEnterpriseServerVersion()).ReturnsAsync("Github AE");
        var result = await _service.AreBlobCredentialsRequired(GHES_API_URL);
        result.Should().Be(true);
    }

    [Fact]
    public async Task Empty_Ghes_Url_Returns_False()
    {
        var result = await _service.AreBlobCredentialsRequired("");
        result.Should().Be(false);
    }
}
