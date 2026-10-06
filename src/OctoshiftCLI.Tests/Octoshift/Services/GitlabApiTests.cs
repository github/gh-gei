using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class GitlabApiTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GitlabClient> _mockGitlabClient = TestHelpers.CreateMock<GitlabClient>();

    private readonly GitlabApi _sut;

    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";

    public GitlabApiTests()
    {
        _sut = new GitlabApi(_mockGitlabClient.Object, GITLAB_SERVER_URL, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task GetServerVersion_Returns_Server_Version()
    {
        var endpoint = $"{GITLAB_SERVER_URL}/api/v4/version";
        var version = "18.11.0-ee";

        var responsePayload = new
        {
            version,
            revision = "abc123",
            enterprise = true
        };

        _mockGitlabClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(responsePayload.ToJson());

        var (actualVersion, enterprise) = await _sut.GetServerVersion();

        actualVersion.Should().Be(version);
        enterprise.Should().BeTrue();
    }

    [Fact]
    public async Task LogServerVersion_Logs_Version_With_Enterprise_Edition()
    {
        var endpoint = $"{GITLAB_SERVER_URL}/api/v4/version";
        var version = "18.11.0-ee";

        var responsePayload = new
        {
            version,
            revision = "abc123",
            enterprise = true
        };

        _mockGitlabClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(responsePayload.ToJson());

        await _sut.LogServerVersion();

        _mockOctoLogger.Verify(m => m.LogInformation($"GitLab version: {version} (Enterprise Edition)"), Times.Once);
    }

    [Fact]
    public async Task LogServerVersion_Logs_Version_With_Community_Edition()
    {
        var endpoint = $"{GITLAB_SERVER_URL}/api/v4/version";
        var version = "18.11.0";

        var responsePayload = new
        {
            version,
            revision = "abc123",
            enterprise = false
        };

        _mockGitlabClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(responsePayload.ToJson());

        await _sut.LogServerVersion();

        _mockOctoLogger.Verify(m => m.LogInformation($"GitLab version: {version} (Community Edition)"), Times.Once);
    }

    [Fact]
    public async Task GetProjects_Excludes_Projects_Shared_Into_The_Group()
    {
        const string groupPath = "my-group";
        var endpoint = $"{GITLAB_SERVER_URL}/api/v4/groups/{Uri.EscapeDataString(groupPath)}/projects?per_page=100";

        var ownedProject = new
        {
            id = 1,
            path = "owned-repo",
            name = "Owned Repo",
            archived = false,
            @namespace = new { full_path = groupPath }
        };
        var sharedInProject = new
        {
            id = 2,
            path = "shared-repo",
            name = "Shared Repo",
            archived = false,
            @namespace = new { full_path = "some-other-group" }
        };

        var response = new object[] { ownedProject, sharedInProject }.ToAsyncJTokenEnumerable();
        _mockGitlabClient.Setup(m => m.GetAllAsync(endpoint)).Returns(response);

        var result = await _sut.GetProjects(groupPath);

        result.Should().BeEquivalentTo(new[] { (Id: 1L, Path: "owned-repo", Name: "Owned Repo", Archived: false) });
        _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Once);
    }
}
