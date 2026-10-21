using System;
using System.Globalization;
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
    public async Task GetProjects_Requests_With_Shared_False_To_Exclude_Projects_Shared_Into_The_Group()
    {
        const string groupPath = "my-group";
        var endpoint = $"{GITLAB_SERVER_URL}/api/v4/groups/{Uri.EscapeDataString(groupPath)}/projects?per_page=100&with_shared=false";

        var ownedProject = new
        {
            id = 1,
            path = "owned-repo",
            name = "Owned Repo",
            archived = false
        };

        var response = new object[] { ownedProject }.ToAsyncJTokenEnumerable();
        _mockGitlabClient.Setup(m => m.GetAllAsync(endpoint)).Returns(response);

        var result = await _sut.GetProjects(groupPath);

        result.Should().BeEquivalentTo(new[] { (Id: 1L, Path: "owned-repo", Name: "Owned Repo", Archived: false) });
    }

    [Theory]
    [InlineData("en-SG", "2026-09-24T12:58:25.000+08:00", 2026, 9, 24, 8)]
    [InlineData("en-GB", "2026-05-09T12:58:25.000+08:00", 2026, 5, 9, 8)]
    [InlineData("de-DE", "2026-09-24T12:58:25.000+00:00", 2026, 9, 24, 0)]
    [InlineData("en-US", "2016-09-20T12:58:25.000-07:00", 2016, 9, 20, -7)]
    public async Task GetRepositoryLatestCommitDate_Parses_Iso8601_Regardless_Of_Current_Culture(
        string culture,
        string committedDate,
        int year,
        int month,
        int day,
        int offsetHours)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            const string groupPath = "my-group";
            const string projectPath = "my-project";
            var encodedProjectPath = Uri.EscapeDataString($"{groupPath}/{projectPath}");
            var endpoint = $"{GITLAB_SERVER_URL}/api/v4/projects/{encodedProjectPath}/repository/commits?per_page=1";
            _mockGitlabClient
                .Setup(m => m.GetOrNullForNotFoundAsync(endpoint))
                .ReturnsAsync($"[{{\"committed_date\":\"{committedDate}\"}}]");

            var result = await _sut.GetRepositoryLatestCommitDate(groupPath, projectPath);

            result.Should().BeExactly(new DateTimeOffset(year, month, day, 12, 58, 25, TimeSpan.FromHours(offsetHours)));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
