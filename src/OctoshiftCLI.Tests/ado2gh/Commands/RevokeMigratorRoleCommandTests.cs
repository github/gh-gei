using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands;

public class RevokeMigratorRoleCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new RevokeMigratorRoleCommand();
        Assert.NotNull(command);
        Assert.Equal("revoke-migrator-role", command.Name);
        Assert.Equal(6, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
    }
}
