using OctoshiftCLI.GithubEnterpriseImporter.Commands.GrantMigratorRole;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.GrantMigratorRole;

public class GrantMigratorRoleCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GrantMigratorRoleCommand();
        Assert.NotNull(command);
        Assert.Equal("grant-migrator-role", command.Name);
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
