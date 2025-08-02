using FluentAssertions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateRepo
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand();

            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(27);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "source-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-uploads-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-bucket-name", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-access-key", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-secret-key", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-session-token", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-region", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-releases", false);
            TestHelpers.VerifyCommandOption(command.Options, "git-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "metadata-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "git-archive-path", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "metadata-archive-path", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "queue-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo-visibility", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "keep-archive", false);
            TestHelpers.VerifyCommandOption(command.Options, "use-github-storage", false, true);
        }
    }
}
