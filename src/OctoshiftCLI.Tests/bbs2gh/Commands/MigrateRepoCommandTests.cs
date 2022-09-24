using FluentAssertions;
using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(18);

            TestHelpers.VerifyCommandOption(command.Options, "bbs-server-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "bbs-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "bbs-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "bbs-username", false);
            TestHelpers.VerifyCommandOption(command.Options, "bbs-password", false);
            TestHelpers.VerifyCommandOption(command.Options, "archive-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "archive-path", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh-user", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh-private-key", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh-port", false);
            TestHelpers.VerifyCommandOption(command.Options, "smb-user", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "smb-password", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }
    }
}
