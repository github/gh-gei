using FluentAssertions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class WaitForMigrationCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new WaitForMigrationCommand();
        command.Should().NotBeNull();
        command.Name.Should().Be("wait-for-migration");
        command.Options.Count.Should().Be(3);

        TestHelpers.VerifyCommandOption(command.Options, "migration-id", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
