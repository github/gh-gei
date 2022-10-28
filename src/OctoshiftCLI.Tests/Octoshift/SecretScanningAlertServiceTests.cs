using System.Threading.Tasks;
using Moq;
using Octoshift.Models;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;
public class SecretScanningAlertServiceTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly SecretScanningAlertService _service;

    private const string SOURCE_ORG = "source-org";
    private const string SOURCE_REPO = "source-repo";
    private const string TARGET_ORG = "target-org";
    private const string TARGET_REPO = "target-repo";

    public SecretScanningAlertServiceTests()
    {
        _service = new SecretScanningAlertService(
            _mockSourceGithubApi.Object,
            _mockTargetGithubApi.Object,
            _mockOctoLogger.Object
        );
    }

    [Fact]
    public async Task One_Secret_Updated()
    {
        var secretType = "custom";
        var secret = "my-password";

        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret
        };

        var targetSecretLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetSecretLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionRevoked)
        );
    }

    [Fact]
    public async Task No_Matching_Location()
    {
        var secretType = "custom";
        var secret = "my-password";

        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret
        };

        var targetSecretLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 99,
            EndLine = 103,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetSecretLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task No_Matching_Secret()
    {
        var secretType = "custom";
        var secret = "my-password";

        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = "some-other-secret"
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { sourceLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Dry_Run_Does_Not_Update()
    {
        var secretType = "custom";
        var secret = "my-password";

        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { sourceLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, true);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Migrates_Multiple_Alerts()
    {
        var secretType = "custom";
        var secretOne = "my-password";
        var secretTwo = "other-password";
        var secretThree = "password-three";

        // Arrange
        var sourceSecretOne = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secretOne,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceSecretTwo = new GithubSecretScanningAlert()
        {
            Number = 2,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secretTwo,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceSecretThree = new GithubSecretScanningAlert()
        {
            Number = 3,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secretThree,
            Resolution = SecretScanningAlert.ResolutionFalsePositive,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            Path = "my-file.txt",
            StartLine = 17,
            EndLine = 18,
            StartColumn = 22,
            EndColumn = 29,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecretOne, sourceSecretTwo, sourceSecretThree });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, It.IsAny<int>()))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecretOne = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secretOne
        };

        var targetSecretThree = new GithubSecretScanningAlert()
        {
            Number = 300,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secretThree
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecretOne, targetSecretThree });

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, It.IsAny<int>()))
            .ReturnsAsync(new[] { sourceLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionRevoked)
        );

        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            300,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionFalsePositive)
        );
    }
}
