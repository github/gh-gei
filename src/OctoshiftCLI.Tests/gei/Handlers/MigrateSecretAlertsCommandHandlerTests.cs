using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;
public class MigrateSecretAlertsCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<SecretScanningAlertService> _mockSecretScanningAlertService = TestHelpers.CreateMock<SecretScanningAlertService>();

    private readonly MigrateSecretAlertsCommandHandler _handler;

    private const string SOURCE_ORG = "foo-source-org";
    private const string SOURCE_REPO = "blah";
    private const string TARGET_ORG = "foo-target-org";

    public MigrateSecretAlertsCommandHandlerTests()
    {
        _handler = new MigrateSecretAlertsCommandHandler(
            _mockOctoLogger.Object,
            _mockSecretScanningAlertService.Object);
    }

    [Fact]
    public async Task Target_Repo_Defaults_To_Source_Repo()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            TargetOrg = TARGET_ORG,
        };

        await _handler.Handle(args);

        _mockSecretScanningAlertService.Verify(m => m.MigrateSecretScanningAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, SOURCE_REPO, false));
    }
}
