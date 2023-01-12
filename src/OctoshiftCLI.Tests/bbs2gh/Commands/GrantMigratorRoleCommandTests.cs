using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class GrantMigratorRoleCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GrantMigratorRoleCommand();
        Assert.NotNull(command);
        Assert.Equal("grant-migrator-role", command.Name);
        Assert.Equal(6, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
    }
}
