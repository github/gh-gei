using Moq;
using Octoshift;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class ReclaimMannequinCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();
    private readonly ReclaimMannequinCommand _command;

    public ReclaimMannequinCommandTests()
    {
        _command = new ReclaimMannequinCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object, _mockReclaimService.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        Assert.NotNull(_command);
        Assert.Equal("reclaim-mannequin", _command.Name);
        Assert.Equal(8, _command.Options.Count);

        TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "csv", false);
        TestHelpers.VerifyCommandOption(_command.Options, "mannequin-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "mannequin-id", false);
        TestHelpers.VerifyCommandOption(_command.Options, "target-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "force", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
