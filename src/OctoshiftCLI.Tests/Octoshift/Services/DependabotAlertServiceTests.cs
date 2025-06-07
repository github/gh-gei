using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.Models;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class DependabotAlertServiceTests
{
    private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly DependabotAlertService _alertService;

    private const string SOURCE_ORG = "SOURCE-ORG";
    private const string SOURCE_REPO = "SOURCE-REPO";
    private const string TARGET_ORG = "TARGET-ORG";
    private const string TARGET_REPO = "TARGET-REPO";

    public DependabotAlertServiceTests()
    {
        _alertService = new DependabotAlertService(_mockSourceGithubApi.Object, _mockTargetGithubApi.Object, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_No_Alerts_Does_Not_Update_Any_Alerts()
    {
        // Arrange
        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(Enumerable.Empty<DependabotAlert>());
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(Enumerable.Empty<DependabotAlert>());

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_Matches_Alerts_By_Ghsa_Id_And_Package()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "dismissed",
            DismissedReason = "false_positive",
            DismissedComment = "Not applicable",
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        var targetAlert = new DependabotAlert
        {
            Number = 2,
            State = "open",
            Url = "https://api.github.com/repos/target-org/target-repo/dependabot/alerts/2",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(new[] { targetAlert });

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert.Number,
            sourceAlert.State,
            sourceAlert.DismissedReason,
            sourceAlert.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_Falls_Back_To_Cve_Id_When_Ghsa_Id_Does_Not_Match()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "dismissed",
            DismissedReason = "not_used",
            DismissedComment = "Package not used in production",
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1111-1111-1111", CveId = "CVE-2023-1234" },
            Dependency = new DependabotAlertDependency { Package = "express", Manifest = "package.json" }
        };

        var targetAlert = new DependabotAlert
        {
            Number = 3,
            State = "open",
            Url = "https://api.github.com/repos/target-org/target-repo/dependabot/alerts/3",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-2222-2222-2222", CveId = "CVE-2023-1234" },
            Dependency = new DependabotAlertDependency { Package = "express", Manifest = "package.json" }
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(new[] { targetAlert });

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(
            TARGET_ORG,
            TARGET_REPO,
            targetAlert.Number,
            sourceAlert.State,
            sourceAlert.DismissedReason,
            sourceAlert.DismissedComment
        ), Times.Once);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_Skips_Non_Migratable_States()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "fixed", // non-migratable state
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(Enumerable.Empty<DependabotAlert>());

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_Skips_When_Target_Already_Has_Same_State()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "dismissed",
            DismissedReason = "false_positive",
            DismissedComment = "Not applicable",
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        var targetAlert = new DependabotAlert
        {
            Number = 2,
            State = "dismissed", // Same state as source
            Url = "https://api.github.com/repos/target-org/target-repo/dependabot/alerts/2",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(new[] { targetAlert });

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false);

        // Assert
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MigrateDependabotAlerts_Throws_When_Target_Alert_Not_Found()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "dismissed",
            DismissedReason = "false_positive",
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        // No matching target alert
        var targetAlert = new DependabotAlert
        {
            Number = 2,
            State = "open",
            Url = "https://api.github.com/repos/target-org/target-repo/dependabot/alerts/2",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-9999-9999-9999" }, // Different GHSA ID
            Dependency = new DependabotAlertDependency { Package = "react", Manifest = "package.json" } // Different package
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });
        _mockTargetGithubApi.Setup(x => x.GetDependabotAlertsForRepository(TARGET_ORG, TARGET_REPO)).ReturnsAsync(new[] { targetAlert });

        // Act & Assert
        await _alertService.Invoking(x => x.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, false))
            .Should().ThrowAsync<OctoshiftCliException>()
            .WithMessage("Migration of Dependabot Alerts failed.");
    }

    [Fact]
    public async Task MigrateDependabotAlerts_In_Dry_Run_Mode_Does_Not_Update_Alerts()
    {
        // Arrange
        var sourceAlert = new DependabotAlert
        {
            Number = 1,
            State = "dismissed",
            DismissedReason = "false_positive",
            Url = "https://api.github.com/repos/source-org/source-repo/dependabot/alerts/1",
            SecurityAdvisory = new DependabotAlertSecurityAdvisory { GhsaId = "GHSA-1234-5678-9abc" },
            Dependency = new DependabotAlertDependency { Package = "lodash", Manifest = "package.json" }
        };

        _mockSourceGithubApi.Setup(x => x.GetDependabotAlertsForRepository(SOURCE_ORG, SOURCE_REPO)).ReturnsAsync(new[] { sourceAlert });

        // Act
        await _alertService.MigrateDependabotAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, true);

        // Assert
        _mockTargetGithubApi.Verify(x => x.GetDependabotAlertsForRepository(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockTargetGithubApi.Verify(x => x.UpdateDependabotAlert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}