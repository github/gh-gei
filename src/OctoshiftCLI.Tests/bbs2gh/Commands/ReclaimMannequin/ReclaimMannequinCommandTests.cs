using OctoshiftCLI.BbsToGithub.Commands.ReclaimMannequin;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new ReclaimMannequinCommand();
        Assert.NotNull(command);
        Assert.Equal("reclaim-mannequin", command.Name);
        Assert.Equal(11, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "csv", false);
        TestHelpers.VerifyCommandOption(command.Options, "mannequin-user", false);
        TestHelpers.VerifyCommandOption(command.Options, "mannequin-id", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-user", false);
        TestHelpers.VerifyCommandOption(command.Options, "force", false);
        TestHelpers.VerifyCommandOption(command.Options, "no-prompt", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "skip-invitation", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
