using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.DownloadLogs;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.DownloadLogs;

public class DownloadLogsCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string GITHUB_ORG = "foo-org";
    private const string GITHUB_REPO = "foo-repo";

    [Fact]
    public void Validate_Throws_When_GithubOrg_Is_Url()
    {
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = "https://github.com/my-org",
            GithubRepo = GITHUB_REPO
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
    }

    [Fact]
    public void Validate_Throws_When_GithubRepo_Is_Url()
    {
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = "http://github.com/org/repo"
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --github-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
    }

    [Fact]
    public void Validate_Succeeds_With_Valid_Names()
    {
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO
        };

        args.Validate(_mockOctoLogger.Object);

        args.GithubOrg.Should().Be(GITHUB_ORG);
        args.GithubRepo.Should().Be(GITHUB_REPO);
    }
}
