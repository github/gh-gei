using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift;
using Octoshift.Models;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;
public class MigrateSecretScanningAlertsCommandTests
{
    private readonly Mock<ISourceGithubApiFactory>
        _mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();

    private readonly Mock<ITargetGithubApiFactory>
        _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();

    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();

    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();

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

    [Fact]
    public async Task Happy_Path()
    {
        // Arrange
        var sourceGithubPat = Guid.NewGuid().ToString();
        var targetGithubPat = Guid.NewGuid().ToString();
        var sourceOrg = "source-org";
        var sourceRepo = "source-repo";
        var targetOrg = "target-org";
        var targetRepo = "target-repo";

        var sourceSecretScanningAlert_Open = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = "custom",
            Secret = "123456789"
        };
        IEnumerable<GithubSecretScanningAlertLocation> sourceSecretScanningAlert_Open_Locations = new[]
        {
                new GithubSecretScanningAlertLocation() { }
            };

        var sourceSecretScanningAlert_Resolved_Revoked = new GithubSecretScanningAlert()
        {
            Number = 2,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = "custom",
            Secret = "123456789abc",
            Resolution = SecretScanningAlert.ResolutionRevoked,
            ResolvedAt = "2022-09-01T00:00:01Z",
            ResolvedBy = "mock-user"
        };

        var sourceSecretScanningAlert_Resolved_WontFix = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = "custom",
            Secret = "123456789abc1234",
            Resolution = SecretScanningAlert.ResolutionWontFix,
            ResolvedAt = "2021-09-01T12:00:00Z",
            ResolvedBy = "mock-user"
        };

        var sourceSecretScanningAlert_Resolved_FalsePositive = new GithubSecretScanningAlert()
        {
            Number = 1001,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = "custom",
            Secret = "abcxyz",
            Resolution = SecretScanningAlert.ResolutionFalsePositive,
            ResolvedAt = "2021-11-30T12:06:11Z",
            ResolvedBy = "mock-user"
        };

        IEnumerable<GithubSecretScanningAlert> sourceSecrets = new[]
        {
                sourceSecretScanningAlert_Open,
                sourceSecretScanningAlert_Resolved_Revoked,
                sourceSecretScanningAlert_Resolved_WontFix,
                sourceSecretScanningAlert_Resolved_FalsePositive
            };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(sourceOrg, sourceRepo).Result)
            .Returns(sourceSecrets);
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(sourceOrg, sourceRepo, sourceSecretScanningAlert_Open.Number))
            .ReturnsAsync(sourceSecretScanningAlert_Open_Locations);

        var targetSecretScanningAlert_Open_One = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = sourceSecretScanningAlert_Resolved_Revoked.SecretType,
            Secret = sourceSecretScanningAlert_Resolved_Revoked.Secret
        };

        var targetSecretScanningAlert_Open_Two = new GithubSecretScanningAlert()
        {
            Number = 200,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = sourceSecretScanningAlert_Open.SecretType,
            Secret = sourceSecretScanningAlert_Open.Secret
        };

        var targetSecretScanningAlert_Open_Three = new GithubSecretScanningAlert()
        {
            Number = 300,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = sourceSecretScanningAlert_Resolved_WontFix.SecretType,
            Secret = sourceSecretScanningAlert_Resolved_WontFix.Secret
        };

        var targetSecretScanningAlert_Open_Four = new GithubSecretScanningAlert()
        {
            Number = sourceSecretScanningAlert_Resolved_FalsePositive.Number,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = sourceSecretScanningAlert_Resolved_FalsePositive.SecretType,
            Secret = sourceSecretScanningAlert_Resolved_FalsePositive.Secret
        };

        IEnumerable<GithubSecretScanningAlert> targetSecrets = new[]
        {
                targetSecretScanningAlert_Open_One,
                targetSecretScanningAlert_Open_Two,
                targetSecretScanningAlert_Open_Three,
                targetSecretScanningAlert_Open_Four
            };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(targetOrg, targetRepo).Result)
            .Returns(targetSecrets);

        _mockSecretScanningAlertServiceFactory.Setup(x =>
            x.Create(It.IsAny<string>(), sourceGithubPat, It.IsAny<string>(), targetGithubPat, false)).Returns(new SecretScanningAlertService(_mockSourceGithubApi.Object, _mockTargetGithubApi.Object, _mockOctoLogger.Object));

        _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

        _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), sourceGithubPat))
            .Returns(_mockSourceGithubApi.Object);
        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), targetGithubPat))
            .Returns(_mockTargetGithubApi.Object);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>()
            {
                "Migrating Repo Secret Scanning Alerts...",
                $"GITHUB SOURCE ORG: {sourceOrg}",
                $"SOURCE REPO: {sourceRepo}",
                $"GITHUB TARGET ORG: {targetOrg}",
                $"TARGET REPO: {targetRepo}",
                "GITHUB SOURCE PAT: ***",
                "GITHUB TARGET PAT: ***",
                $"Migrating Secret Scanning Alerts from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'",
                $"Source {sourceOrg}/{sourceRepo} secret alerts found: 4",
                $"Target {targetOrg}/{targetRepo} secret alerts found: 4",
                "Matching secret resolutions from source to target repository",
                "Processing source secret 1",
                "  secret alert is still open, nothing to do",
                "Processing source secret 2",
                "  secret is resolved, looking for matching detection in target...",
                $"  source secret alert matched alert to 100 in {targetOrg}/{targetRepo}.",
                "  updating target alert:100 to state:resolved and resolution:revoked",
                "  source and target alert state and resolution have been aligned to revoked.",
                "Processing source secret 100",
                "  secret is resolved, looking for matching detection in target...",
                $"  source secret alert matched alert to 300 in {targetOrg}/{targetRepo}.",
                "  updating target alert:300 to state:resolved and resolution:wont_fix",
                "  source and target alert state and resolution have been aligned to wont_fix.",
                "Processing source secret 1001",
                "  secret is resolved, looking for matching detection in target...",
                $"  source secret alert matched alert to 1001 in {targetOrg}/{targetRepo}.",
                "  updating target alert:1001 to state:resolved and resolution:false_positive",
                "  source and target alert state and resolution have been aligned to false_positive.",
                "Secret Scanning results completed."
            };

        // Act
        var args = new MigrateSecretScanningAlertsCommandArgs()
        {
            SourceOrg = sourceOrg,
            SourceRepo = sourceRepo,
            GithubSourcePat = sourceGithubPat,
            TargetOrg = targetOrg,
            TargetRepo = targetRepo,
            GithubTargetPat = targetGithubPat
        };
        await _command.Invoke(args);

        // Assert
        _mockSourceGithubApi.Verify(m => m.GetSecretScanningAlertsForRepository(sourceOrg, sourceRepo));
        _mockSourceGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(sourceOrg, sourceRepo, sourceSecretScanningAlert_Open.Number));
        _mockSourceGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(sourceOrg, sourceRepo, sourceSecretScanningAlert_Resolved_Revoked.Number));
        _mockSourceGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(sourceOrg, sourceRepo, sourceSecretScanningAlert_Resolved_FalsePositive.Number));
        _mockSourceGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(sourceOrg, sourceRepo, sourceSecretScanningAlert_Resolved_WontFix.Number));

        _mockTargetGithubApi.Verify(m => m.GetSecretScanningAlertsForRepository(targetOrg, targetRepo));
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            targetOrg,
            targetRepo,
            targetSecretScanningAlert_Open_One.Number,
            SecretScanningAlert.AlertStateResolved,
            sourceSecretScanningAlert_Resolved_Revoked.Resolution)
        );
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            targetOrg,
            targetRepo,
            targetSecretScanningAlert_Open_Three.Number,
            SecretScanningAlert.AlertStateResolved,
            sourceSecretScanningAlert_Resolved_WontFix.Resolution)
        );
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            targetOrg,
            targetRepo,
            targetSecretScanningAlert_Open_Four.Number,
            SecretScanningAlert.AlertStateResolved,
            sourceSecretScanningAlert_Resolved_FalsePositive.Resolution)
        );
        _mockTargetGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(targetOrg, targetRepo, targetSecretScanningAlert_Open_One.Number));
        _mockTargetGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(targetOrg, targetRepo, targetSecretScanningAlert_Open_Two.Number));
        _mockTargetGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(targetOrg, targetRepo, targetSecretScanningAlert_Open_Three.Number));
        _mockTargetGithubApi.Verify(m => m.GetSecretScanningAlertsLocations(targetOrg, targetRepo, targetSecretScanningAlert_Open_Four.Number));

        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(24));
        _mockOctoLogger.Verify(m => m.LogVerbose(It.IsAny<string>()), Times.Exactly(3));
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Exactly(5));
        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockSourceGithubApi.VerifyNoOtherCalls();
        _mockTargetGithubApi.VerifyNoOtherCalls();
        _mockOctoLogger.VerifyNoOtherCalls();
    }
}
