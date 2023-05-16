using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.WaitForMigration;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.WaitForMigration;

public class WaitForMigrationCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    [Fact]
    public void With_Invalid_Migration_ID_Prefix_Throws_Exception()
    {
        var invalidId = "SomeId";

        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = invalidId,
        };

        FluentActions
            .Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .Throw<OctoshiftCliException>()
            .WithMessage($"Invalid migration id: {invalidId}");
    }
}

