using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateSecretScanningAlertsCommandTests
    {
        private readonly Mock<ISourceGithubApiFactory>
            _mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();

        private readonly Mock<ITargetGithubApiFactory>
            _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider =
            TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly Mock<ISecretScanningAlertServiceFactory> _mockSecretScanningAlertServiceFactory =
            new Mock<ISecretScanningAlertServiceFactory>();

        private readonly MigrateSecretScanningAlertsCommand _command;

        public MigrateSecretScanningAlertsCommandTests()
        {
            _command = new MigrateSecretScanningAlertsCommand(
                _mockOctoLogger.Object,
                _mockSecretScanningAlertServiceFactory.Object,
                _mockSourceGithubApiFactory.Object,
                _mockTargetGithubApiFactory.Object,
                _mockEnvironmentVariableProvider.Object
            );
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("migrate-secret-alerts");
            _command.Options.Count.Should().Be(11);

            TestHelpers.VerifyCommandOption(_command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(_command.Options, "source-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "target-repo", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(_command.Options, "dry-run", false);
        }
    }
}
