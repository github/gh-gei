using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateSecretAlerts;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateSecretAlerts;

public class MigrateSecretAlertsCommandTests
{
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<SecretScanningAlertServiceFactory> _mockSecretScanningAlertServiceFactory = TestHelpers.CreateMock<SecretScanningAlertServiceFactory>();

    private readonly ServiceProvider _serviceProvider;
    private readonly MigrateSecretAlertsCommand _command = [];

    public MigrateSecretAlertsCommandTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockEnvironmentVariableProvider.Object)
            .AddSingleton(_mockSecretScanningAlertServiceFactory.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("migrate-secret-alerts");
        _command.Options.Count.Should().Be(11);

        TestHelpers.VerifyCommandOption(_command.Options, "source-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "source-repo", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-repo", false);
        TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ghes-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-source-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(_command.Options, "dry-run", false);
    }

    [Fact]
    public void Should_Pass_Target_Pat_From_Cli_Args_To_Factory()
    {
        var targetToken = "target-token";

        var args = new MigrateSecretAlertsCommandArgs()
        {
            SourceOrg = "source-org",
            SourceRepo = "source-repo",
            TargetOrg = "target-org",
            TargetRepo = "target-repo",
            GithubTargetPat = targetToken,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockSecretScanningAlertServiceFactory.Verify(m => m.Create(It.IsAny<string>(), targetToken, It.IsAny<string>(), targetToken, It.IsAny<bool>()));
    }

    [Fact]
    public void Should_Pass_Source_Pat_From_Cli_Args_To_Factory()
    {
        var sourceToken = "source-token";
        var targetToken = "target-token";

        var args = new MigrateSecretAlertsCommandArgs()
        {
            SourceOrg = "source-org",
            SourceRepo = "source-repo",
            TargetOrg = "target-org",
            TargetRepo = "target-repo",
            GithubSourcePat = sourceToken,
            GithubTargetPat = targetToken,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockSecretScanningAlertServiceFactory.Verify(m => m.Create(It.IsAny<string>(), sourceToken, It.IsAny<string>(), targetToken, It.IsAny<bool>()));
    }
}
