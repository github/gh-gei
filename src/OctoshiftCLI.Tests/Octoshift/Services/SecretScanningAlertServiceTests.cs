using System;
using System.Threading.Tasks;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

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
            LocationType = "commit",
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
            LocationType = "commit",
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
            SecretScanningAlert.ResolutionRevoked,
            null)
        );
    }

    [Fact]
    public async Task Secret_Updated_With_Comment()
    {
        var secretType = "custom";
        var secret = "my-password";
        var resolutionComment = "This secret was revoked and replaced";

        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
            ResolutionComment = resolutionComment
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            LocationType = "commit",
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
            LocationType = "commit",
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
            SecretScanningAlert.ResolutionRevoked,
            resolutionComment)
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
            LocationType = "commit",
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
            LocationType = "commit",
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
            LocationType = "commit",
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
            LocationType = "commit",
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
            SecretScanningAlert.ResolutionRevoked,
            null)
        );

        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            300,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionFalsePositive,
            null)
        );
    }

    [Fact]
    public async Task Matching_Alerts_With_Different_Location_Types_Are_Not_Matched()
    {
        // Arrange
        var secretType = "custom";
        var secret = "my-password";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "commit",
            Path = "my-file.txt",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 5,
            EndColumn = 15,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "issue_title",
            IssueTitleUrl = "https://api.github.com/repos/target-org/target-repo/issues/1"
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Alerts_With_Different_Number_Of_Locations_Are_Not_Matched()
    {
        // Arrange
        var secretType = "custom";
        var secret = "multi-location-secret";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 2,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "commit",
                Path = "file1.txt",
                StartLine = 10,
                EndLine = 10,
                StartColumn = 5,
                EndColumn = 15,
                BlobSha = "abc123"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "issue_title",
                IssueTitleUrl = "https://api.github.com/repos/source-org/source-repo/issues/1"
            }
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 2))
            .ReturnsAsync(sourceLocations);

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 200,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "commit",
                Path = "file1.txt",
                StartLine = 10,
                EndLine = 10,
                StartColumn = 5,
                EndColumn = 15,
                BlobSha = "abc123"
            }
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 200))
            .ReturnsAsync(targetLocations);

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task No_Alerts_In_Source_Repository()
    {
        // Arrange
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(Array.Empty<GithubSecretScanningAlert>());

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task No_Alerts_In_Target_Repository()
    {
        // Arrange
        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = "custom",
            Secret = "my-password",
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation()
        {
            LocationType = "commit",
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

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(Array.Empty<GithubSecretScanningAlert>());

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Migrate_Matching_Alerts_With_Different_Resolutions()
    {
        // Arrange
        var secretType = "custom";
        var secret = "my-password";

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
            LocationType = "commit",
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
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionFalsePositive,
        };

        var targetSecretLocation = new GithubSecretScanningAlertLocation()
        {
            LocationType = "commit",
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
            SecretScanningAlert.ResolutionRevoked,
            null)
        );
    }

    [Fact]
    public async Task Matching_Alerts_With_Different_Locations_Are_Not_Matched()
    {
        // Arrange
        var secretType = "custom";
        var secret = "my-password";

        var sourceSecret = new GithubSecretScanningAlert()
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocations = new[]
        {
            new GithubSecretScanningAlertLocation()
            {
                LocationType = "commit",
                Path = "my-file.txt",
                StartLine = 17,
                EndLine = 18,
                StartColumn = 22,
                EndColumn = 29,
                BlobSha = "abc123"
            },
            new GithubSecretScanningAlertLocation()
            {
                LocationType = "issue_title",
                IssueTitleUrl = "https://api.github.com/repos/source-org/source-repo/issues/1"
            }
        };

        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(sourceLocations);

        var targetSecret = new GithubSecretScanningAlert()
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var targetLocations = new[]
        {
            new GithubSecretScanningAlertLocation()
            {
                LocationType = "commit",
                Path = "my-file.txt",
                StartLine = 17,
                EndLine = 18,
                StartColumn = 22,
                EndColumn = 29,
                BlobSha = "different-sha"
            },
            new GithubSecretScanningAlertLocation()
            {
                LocationType = "issue_title",
                IssueTitleUrl = "https://api.github.com/repos/target-org/target-repo/issues/1"
            }
        };

        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi.Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(targetLocations);

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Alerts_With_Extra_Fields_Are_Handled_Gracefully()
    {
        // Arrange
        var secretType = "custom";
        var secret = "my-password";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "commit",
            Path = "my-file.txt",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 5,
            EndColumn = 15,
            BlobSha = "abc123"
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "commit",
            Path = "my-file.txt",
            StartLine = 10,
            EndLine = 10,
            StartColumn = 5,
            EndColumn = 15,
            BlobSha = "abc123",
            IssueTitleUrl = "https://api.github.com/repos/target-org/target-repo/issues/1" // Extra field
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionRevoked,
            null)
        );
    }

    [Fact]
    public async Task Pull_Request_Comment_Location_Is_Matched_And_Secret_Is_Updated()
    {
        // Arrange
        var secretType = "github_personal_access_token";
        var secret = "gho_abcdefghijklmnopqrstuvwxyz";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
            ResolutionComment = "This token was revoked during migration"
        };

        var sourceLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "pull_request_comment",
            PullRequestCommentUrl = "https://api.github.com/repos/source-org/source-repo/issues/comments/123456789"
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "pull_request_comment",
            PullRequestCommentUrl = "https://api.github.com/repos/target-org/target-repo/issues/comments/123456789"
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionRevoked,
            "This token was revoked during migration")
        );
    }

    [Fact]
    public async Task Pull_Request_Comment_And_Commit_Locations_Are_Both_Matched()
    {
        // Arrange
        var secretType = "github_personal_access_token";
        var secret = "gho_abcdefghijklmnopqrstuvwxyz";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_comment",
                PullRequestCommentUrl = "https://api.github.com/repos/source-org/source-repo/issues/comments/123456789"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "commit",
                Path = "storage/src/main/resources/.env",
                StartLine = 6,
                EndLine = 6,
                StartColumn = 17,
                EndColumn = 49,
                BlobSha = "40ecdbab769bc2cb0e4e2114fd6986ae1acc3df2"
            }
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(sourceLocations);

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_comment",
                PullRequestCommentUrl = "https://api.github.com/repos/target-org/target-repo/issues/comments/123456789"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "commit",
                Path = "storage/src/main/resources/.env",
                StartLine = 6,
                EndLine = 6,
                StartColumn = 17,
                EndColumn = 49,
                BlobSha = "40ecdbab769bc2cb0e4e2114fd6986ae1acc3df2"
            }
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(targetLocations);

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionRevoked,
            null)
        );
    }

    [Fact]
    public async Task Different_Pull_Request_Comment_Urls_Are_Not_Matched()
    {
        // Arrange
        var secretType = "github_personal_access_token";
        var secret = "gho_abcdefghijklmnopqrstuvwxyz";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionRevoked,
        };

        var sourceLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "pull_request_comment",
            PullRequestCommentUrl = "https://api.github.com/repos/source-org/source-repo/issues/comments/123456789"
        };

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(new[] { sourceLocation });

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocation = new GithubSecretScanningAlertLocation
        {
            LocationType = "pull_request_comment",
            PullRequestCommentUrl = "https://api.github.com/repos/target-org/target-repo/issues/comments/987654321"
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(new[] { targetLocation });

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Multiple_Pull_Request_Related_Location_Types_Are_Matched()
    {
        // Arrange
        var secretType = "github_personal_access_token";
        var secret = "gho_abcdefghijklmnopqrstuvwxyz";

        var sourceSecret = new GithubSecretScanningAlert
        {
            Number = 1,
            State = SecretScanningAlert.AlertStateResolved,
            SecretType = secretType,
            Secret = secret,
            Resolution = SecretScanningAlert.ResolutionFalsePositive,
            ResolutionComment = "This is a test token"
        };

        var sourceLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_comment",
                PullRequestCommentUrl = "https://api.github.com/repos/source-org/source-repo/issues/comments/123456789"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_body",
                PullRequestBodyUrl = "https://api.github.com/repos/source-org/source-repo/pulls/42"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_review",
                PullRequestReviewUrl = "https://api.github.com/repos/source-org/source-repo/pulls/42/reviews/123"
            }
        };

        _mockOctoLogger
            .Setup(x => x.LogInformation(It.IsAny<string>()))
            .Callback<string>(message => Console.WriteLine(message));

        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO))
            .ReturnsAsync(new[] { sourceSecret });
        _mockSourceGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(SOURCE_ORG, SOURCE_REPO, 1))
            .ReturnsAsync(sourceLocations);

        var targetSecret = new GithubSecretScanningAlert
        {
            Number = 100,
            State = SecretScanningAlert.AlertStateOpen,
            SecretType = secretType,
            Secret = secret,
        };

        var targetLocations = new[]
        {
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_comment",
                PullRequestCommentUrl = "https://api.github.com/repos/target-org/target-repo/issues/comments/123456789"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_body",
                PullRequestBodyUrl = "https://api.github.com/repos/target-org/target-repo/pulls/42"
            },
            new GithubSecretScanningAlertLocation
            {
                LocationType = "pull_request_review",
                PullRequestReviewUrl = "https://api.github.com/repos/target-org/target-repo/pulls/42/reviews/123"
            }
        };

        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsForRepository(TARGET_ORG, TARGET_REPO))
            .ReturnsAsync(new[] { targetSecret });
        _mockTargetGithubApi
            .Setup(x => x.GetSecretScanningAlertsLocations(TARGET_ORG, TARGET_REPO, 100))
            .ReturnsAsync(targetLocations);

        // Act
        await _service.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(m => m.UpdateSecretScanningAlert(
            TARGET_ORG,
            TARGET_REPO,
            100,
            SecretScanningAlert.AlertStateResolved,
            SecretScanningAlert.ResolutionFalsePositive,
            "This is a test token")
        );
    }
}
