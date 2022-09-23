using FluentAssertions;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(16);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "download-migration-logs", false);
            TestHelpers.VerifyCommandOption(command.Options, "create-teams", false);
            TestHelpers.VerifyCommandOption(command.Options, "link-idp-groups", false);
            TestHelpers.VerifyCommandOption(command.Options, "lock-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "disable-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "integrate-boards", false);
            TestHelpers.VerifyCommandOption(command.Options, "rewire-pipelines", false);
            TestHelpers.VerifyCommandOption(command.Options, "all", false);
            TestHelpers.VerifyCommandOption(command.Options, "repo-list", false);
        }
    }
}
