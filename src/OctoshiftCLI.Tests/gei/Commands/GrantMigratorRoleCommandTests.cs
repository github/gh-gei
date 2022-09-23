using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class GrantMigratorRoleCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GrantMigratorRoleCommand(null, null);
        Assert.NotNull(command);
        Assert.Equal("grant-migrator-role", command.Name);
        Assert.Equal(5, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
