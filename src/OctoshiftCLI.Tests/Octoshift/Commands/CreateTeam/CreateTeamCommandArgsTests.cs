using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.CreateTeam;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.CreateTeam;

public class CreateTeamCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string GITHUB_ORG = "foo-org";
    private const string TEAM_NAME = "my-team";

    [Fact]
    public void Validate_Throws_When_GithubOrg_Is_Url()
    {
        var args = new CreateTeamCommandArgs
        {
            GithubOrg = "http://github.com/my-org",
            TeamName = TEAM_NAME
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
    }

    [Fact]
    public void Validate_Succeeds_With_Valid_Name()
    {
        var args = new CreateTeamCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            TeamName = TEAM_NAME
        };

        args.Validate(_mockOctoLogger.Object);

        args.GithubOrg.Should().Be(GITHUB_ORG);
    }
}
