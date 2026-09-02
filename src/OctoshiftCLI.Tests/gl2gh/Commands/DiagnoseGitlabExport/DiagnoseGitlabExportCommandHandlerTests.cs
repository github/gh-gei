using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

    private readonly DiagnoseGitlabExportCommandHandler _handler;

    public DiagnoseGitlabExportCommandHandlerTests()
    {
        _handler = new DiagnoseGitlabExportCommandHandler(_mockOctoLogger.Object, _mockGitlabApi.Object, _mockFileSystemProvider.Object);
    }

    [Fact]
    public async Task Handle_Writes_Report_With_Export_Status_And_Gitlab_Admin_Commands()
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = "https://gitlab.contoso.com",
            GitlabGroup = "parent/group",
            GitlabProject = "project",
            Output = "diagnostics.md"
        };
        string report = null;

        _mockGitlabApi.Setup(m => m.GetServerVersion()).ReturnsAsync(("18.11.0-ee", true));
        _mockGitlabApi.Setup(m => m.GetProjectDetails("parent/group", "project"))
            .ReturnsAsync(new GitlabProjectDetails(123, "parent/group/project", "https://gitlab.contoso.com/parent/group/project", false, "private", 42, 43, 44));
        _mockGitlabApi.Setup(m => m.GetExportDetails("parent/group", "project"))
            .ReturnsAsync(new GitlabExportDetails(123, "failed", null, "{\"export_status\":\"failed\"}"));
        _mockFileSystemProvider.Setup(m => m.WriteAllTextAsync("diagnostics.md", It.IsAny<string>()))
            .Callback<string, string>((_, contents) => report = contents)
            .Returns(Task.CompletedTask);

        await _handler.Handle(args);

        Assert.Contains("Export status: failed", report);
        Assert.Contains("Project.find_by_full_path('parent/group/project')", report);
        Assert.Contains("/var/log/gitlab/sidekiq/current", report);
        Assert.Contains("/var/log/gitlab/gitlab-rails/importer.log", report);
        _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(s => s.Contains("GitLab reported the project export as failed"))), Times.Once);
        _mockOctoLogger.Verify(m => m.LogSuccess("Wrote GitLab export diagnostics to diagnostics.md."), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_When_Output_Exists_Without_Overwrite()
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = "https://gitlab.contoso.com",
            GitlabGroup = "parent/group",
            GitlabProject = "project",
            Output = "diagnostics.md"
        };

        _mockFileSystemProvider.Setup(m => m.FileExists("diagnostics.md")).Returns(true);

        var ex = await Assert.ThrowsAsync<OctoshiftCliException>(() => _handler.Handle(args));

        Assert.Equal("File diagnostics.md already exists! Use --overwrite to overwrite this file.", ex.Message);
    }
}
