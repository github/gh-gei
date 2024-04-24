using OctoshiftCLI.GithubEnterpriseImporter.Commands.CreateTeam;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.CreateTeam;

public class CreateTeamCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new CreateTeamCommand();
        Assert.NotNull(command);
        Assert.Equal("create-team", command.Name);
        Assert.Equal(6, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "team-name", true);
        TestHelpers.VerifyCommandOption(command.Options, "idp-group", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
    }
}
