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

    [Fact]
    public void Validate_Throws_When_SourceOrg_Is_Url()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = "https://github.com/my-org",
            SourceRepo = SOURCE_REPO,
            TargetOrg = TARGET_ORG,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
    }

    [Fact]
    public void Validate_Throws_When_TargetOrg_Is_Url()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            TargetOrg = "http://github.com/my-org",
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
    }

    [Fact]
    public void Validate_Throws_When_SourceRepo_Is_Url()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = SOURCE_ORG,
            SourceRepo = "http://github.com/org/repo",
            TargetOrg = TARGET_ORG,
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --source-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
    }

    [Fact]
    public void Validate_Throws_When_TargetRepo_Is_Url()
    {
        var args = new MigrateSecretAlertsCommandArgs
        {
            SourceOrg = SOURCE_ORG,
            SourceRepo = SOURCE_REPO,
            TargetOrg = TARGET_ORG,
            TargetRepo = "http://github.com/org/repo"
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --target-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
    }
}
