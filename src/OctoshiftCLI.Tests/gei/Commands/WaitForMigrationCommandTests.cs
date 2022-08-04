using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class WaitForMigrationCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly WaitForMigrationCommand _command;

    private const int WAIT_INTERVAL = 1;

    public WaitForMigrationCommandTests()
    {
        _command = new WaitForMigrationCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object)
        {
            WaitIntervalInSeconds = WAIT_INTERVAL
        };
    }

    [Fact]
    public void Should_Have_Overridden_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("wait-for-migration");
        _command.Options.Count.Should().Be(3);

        TestHelpers.VerifyCommandOption(_command.Options, "migration-id", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
