using FluentAssertions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateOrg;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateOrgCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateOrgCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-org");
            command.Options.Count.Should().Be(8);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-enterprise", true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "queue-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }
    }
}
