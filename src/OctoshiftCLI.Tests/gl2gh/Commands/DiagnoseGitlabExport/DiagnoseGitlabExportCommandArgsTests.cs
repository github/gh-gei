using FluentAssertions;
using OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandArgsTests
{
    private readonly OctoLogger _log = new();

    [Theory]
    [InlineData(null, "group", "project", "--gitlab-server-url must be provided.")]
    [InlineData("https://gitlab.contoso.com", null, "project", "--gitlab-group must be provided.")]
    [InlineData("https://gitlab.contoso.com", "group", null, "--gitlab-project must be provided.")]
    public void Validate_Requires_Gitlab_Project_Inputs(string gitlabServerUrl, string gitlabGroup, string gitlabProject, string expectedMessage)
    {
        var args = new DiagnoseGitlabExportCommandArgs
        {
            GitlabServerUrl = gitlabServerUrl,
            GitlabGroup = gitlabGroup,
            GitlabProject = gitlabProject
        };

        var ex = Assert.Throws<OctoshiftCliException>(() => args.Validate(_log));
        ex.Message.Should().Be(expectedMessage);
    }
}
