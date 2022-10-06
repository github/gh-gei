using FluentAssertions;
using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class GenerateScriptCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GenerateScriptCommand();
        command.Should().NotBeNull();
        command.Name.Should().Be("generate-script");
        command.Options.Count.Should().Be(10);

        TestHelpers.VerifyCommandOption(command.Options, "bbs-server-url", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-username", false);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-password", false);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-shared-home", false);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-user", true);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-private-key", true);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(command.Options, "output", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
