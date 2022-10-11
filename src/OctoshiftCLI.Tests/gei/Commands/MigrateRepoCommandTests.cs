using FluentAssertions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand();

            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(23);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-server-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-source-org", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "source-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-bucket-name", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-access-key", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-secret-key", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-releases", false);
            TestHelpers.VerifyCommandOption(command.Options, "git-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "metadata-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }
    }
}
