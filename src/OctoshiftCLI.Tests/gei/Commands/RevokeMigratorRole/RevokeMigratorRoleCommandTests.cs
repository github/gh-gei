using OctoshiftCLI.GithubEnterpriseImporter.Commands.RevokeMigratorRole;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.RevokeMigratorRole;

public class RevokeMigratorRoleCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new RevokeMigratorRoleCommand();
        Assert.NotNull(command);
        Assert.Equal("revoke-migrator-role", command.Name);
        Assert.Equal(7, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor", true);
        TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
    }
}
