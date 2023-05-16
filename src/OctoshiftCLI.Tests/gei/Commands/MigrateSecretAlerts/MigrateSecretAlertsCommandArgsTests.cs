using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateSecretAlerts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateSecretAlerts;

public class MigrateSecretAlertsCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string SOURCE_ORG = "foo-source-org";
    private const string SOURCE_REPO = "blah";
    private const string TARGET_ORG = "foo-target-org";

    [Fact]
    public void Target_Repo_Defaults_To_Source_Repo()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            TargetOrg = TARGET_ORG,
        };

        args.Validate(_mockOctoLogger.Object);

        args.TargetRepo.Should().Be(SOURCE_REPO);
    }
}
