using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.ReclaimMannequin;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string GITHUB_ORG = "FooOrg";
    private const string MANNEQUIN_USER = "mona";
    private const string TARGET_USER = "mona_emu";

    [Fact]
    public void No_Parameters_Provided_Throws_OctoshiftCliException()
    {
        var args = new ReclaimMannequinCommandArgs { GithubOrg = GITHUB_ORG };

        FluentActions
            .Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should().Throw<OctoshiftCliException>();
    }

    [Fact]
    public void Skip_Invitation_Without_CSV_Throws_Error()
    {
        // Arrange
        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            MannequinUser = MANNEQUIN_USER,
            TargetUser = TARGET_USER,
        };

        // Act/ Assert
        FluentActions
            .Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should().Throw<OctoshiftCliException>();
    }
}
