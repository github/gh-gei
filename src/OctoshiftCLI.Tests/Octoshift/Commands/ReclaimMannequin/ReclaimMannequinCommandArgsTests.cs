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

    [Fact]
    public void No_Parameters_Provided_Throws_OctoshiftCliException()
    {
        var args = new ReclaimMannequinCommandArgs { GithubOrg = GITHUB_ORG };

        FluentActions
            .Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should().Throw<OctoshiftCliException>();
    }
}
