using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class CreateTeamCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new CreateTeamCommand();
        Assert.NotNull(command);
        Assert.Equal("create-team", command.Name);
        Assert.Equal(5, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "team-name", true);
        TestHelpers.VerifyCommandOption(command.Options, "idp-group", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
