using FluentAssertions;
using OctoshiftCLI.AdoToGithub.Commands.WaitForMigration;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.WaitForMigration;

public class WaitForMigrationCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new WaitForMigrationCommand();
        command.Should().NotBeNull();
        command.Name.Should().Be("wait-for-migration");
        command.Options.Count.Should().Be(4);

        TestHelpers.VerifyCommandOption(command.Options, "migration-id", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
    }
}
