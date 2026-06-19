using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub;
using OctoshiftCLI.GitlabToGithub.Commands.InventoryReport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandlerTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_GROUP = "foo-group";
    private const string GITLAB_PAT = "gitlab-pat";
    private const bool NO_SSL_VERIFY = true;

    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly Mock<GitlabInspectorService> _mockGitlabInspectorService = TestHelpers.CreateMock<GitlabInspectorService>();
    private readonly Mock<GroupsCsvGeneratorService> _mockGroupsCsvGenerator = TestHelpers.CreateMock<GroupsCsvGeneratorService>();
    private readonly Mock<ProjectsCsvGeneratorService> _mockProjectsCsvGenerator = TestHelpers.CreateMock<ProjectsCsvGeneratorService>();

    private string _groupsCsvOutput = "";
    private string _projectsCsvOutput = "";

    private readonly InventoryReportCommandHandler _handler;

    public InventoryReportCommandHandlerTests()
    {
        _handler = new InventoryReportCommandHandler(
            TestHelpers.CreateMock<OctoLogger>().Object,
            _mockGitlabApi.Object,
            _mockGitlabInspectorService.Object,
            _mockGroupsCsvGenerator.Object,
            _mockProjectsCsvGenerator.Object)
        {
            WriteToFile = (path, contents) =>
            {
                if (path == "groups.csv")
                {
                    _groupsCsvOutput = contents;
                }

                if (path == "projects.csv")
                {
                    _projectsCsvOutput = contents;
                }

                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task Happy_Path()
    {
        var expectedGroupsCsv = "groups csv stuff";
        var expectedProjectsCsv = "projects csv stuff";

        _mockGitlabApi.Setup(m => m.GetGroups()).ReturnsAsync(new[] { (Id: 1L, Path: GITLAB_GROUP, Name: "Foo Group") });
        _mockGitlabInspectorService.Setup(m => m.GetProjectCount(It.IsAny<string[]>())).ReturnsAsync(1);

        _mockGroupsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, false)).ReturnsAsync(expectedGroupsCsv);
        _mockProjectsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, false)).ReturnsAsync(expectedProjectsCsv);

        var args = new InventoryReportCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            NoSslVerify = NO_SSL_VERIFY
        };
        await _handler.Handle(args);

        _groupsCsvOutput.Should().Be(expectedGroupsCsv);
        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
    }

    [Fact]
    public async Task Scoped_To_Single_Group()
    {
        var expectedGroupsCsv = "groups csv stuff";
        var expectedProjectsCsv = "projects csv stuff";

        _mockGitlabInspectorService.Setup(m => m.GetProjectCount(GITLAB_GROUP)).ReturnsAsync(1);
        _mockGroupsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, GITLAB_GROUP, false)).ReturnsAsync(expectedGroupsCsv);
        _mockProjectsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, GITLAB_GROUP, false)).ReturnsAsync(expectedProjectsCsv);

        var args = new InventoryReportCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabGroup = GITLAB_GROUP,
            GitlabPat = GITLAB_PAT,
            NoSslVerify = NO_SSL_VERIFY
        };
        await _handler.Handle(args);

        _groupsCsvOutput.Should().Be(expectedGroupsCsv);
        _projectsCsvOutput.Should().Be(expectedProjectsCsv);

        _mockGitlabApi.Verify(m => m.GetGroups(), Times.Never);
    }

    [Fact]
    public async Task It_Generates_Minimal_Csvs_When_Requested()
    {
        var expectedGroupsCsv = "groups csv stuff";
        var expectedProjectsCsv = "projects csv stuff";

        _mockGitlabApi.Setup(m => m.GetGroups()).ReturnsAsync(new[] { (Id: 1L, Path: GITLAB_GROUP, Name: "Foo Group") });
        _mockGitlabInspectorService.Setup(m => m.GetProjectCount(It.IsAny<string[]>())).ReturnsAsync(1);

        _mockGroupsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, It.IsAny<bool>())).ReturnsAsync(expectedGroupsCsv);
        _mockProjectsCsvGenerator.Setup(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, It.IsAny<bool>())).ReturnsAsync(expectedProjectsCsv);

        var args = new InventoryReportCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            NoSslVerify = NO_SSL_VERIFY,
            Minimal = true
        };
        await _handler.Handle(args);

        _groupsCsvOutput.Should().Be(expectedGroupsCsv);
        _projectsCsvOutput.Should().Be(expectedProjectsCsv);

        _mockGroupsCsvGenerator.Verify(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, true));
        _mockProjectsCsvGenerator.Verify(m => m.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, null, true));
    }
}
