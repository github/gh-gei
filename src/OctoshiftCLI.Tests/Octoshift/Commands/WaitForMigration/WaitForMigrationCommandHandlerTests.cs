using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.WaitForMigration;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.WaitForMigration;

public class WaitForMigrationCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly WarningsCountLogger _warningsCountLogger;
    private readonly WaitForMigrationCommandHandler _handler;

    private const string REPO_MIGRATION_ID = "RM_MIGRATION_ID";
    private const string ORG_MIGRATION_ID = "OM_MIGRATION_ID";
    private const string TARGET_REPO = "TARGET_REPO";
    private const string MIGRATION_URL = "URL";
    private const int WAIT_INTERVAL = 0;

    public WaitForMigrationCommandHandlerTests()
    {
        _warningsCountLogger = new WarningsCountLogger(_mockOctoLogger.Object);
        _handler = new WaitForMigrationCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object, _warningsCountLogger)
        {
            WaitIntervalInSeconds = WAIT_INTERVAL
        };

        TestHelpers.SetCliContext();
    }

    [Fact]
    public async Task With_Migration_ID_That_Succeeds_With_No_Warnings()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL));
        _mockGithubApi.Setup(x => x.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((MIGRATION_URL, REPO_MIGRATION_ID));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Waiting for migration of repository {TARGET_REPO} to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} succeeded for {TARGET_REPO}",
                $"Migration log available at {MIGRATION_URL} or by running `gh {TestHelpers.CLI_ROOT_COMMAND} download-logs`"
    };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Migration_ID_That_Succeeds_With_1_Warning()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 1, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, WarningsCount: 1, FailureReason: null, MigrationLogUrl: MIGRATION_URL));
        _mockGithubApi.Setup(x => x.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((MIGRATION_URL, REPO_MIGRATION_ID));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Waiting for migration of repository {TARGET_REPO} to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} succeeded for {TARGET_REPO}",
                "1 warning encountered during this migration",
                $"Migration log available at {MIGRATION_URL} or by running `gh {TestHelpers.CLI_ROOT_COMMAND} download-logs`"
    };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Once);
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Migration_ID_That_Succeeds_With_4_Warnings()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 2, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, WarningsCount: 4, FailureReason: null, MigrationLogUrl: MIGRATION_URL));
        _mockGithubApi.Setup(x => x.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((MIGRATION_URL, REPO_MIGRATION_ID));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Waiting for migration of repository {TARGET_REPO} to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} succeeded for {TARGET_REPO}",
                "4 warnings encountered during this migration",
                $"Migration log available at {MIGRATION_URL} or by running `gh {TestHelpers.CLI_ROOT_COMMAND} download-logs`"
    };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Once);
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Migration_ID_That_Fails()
    {
        // Arrange
        const string failureReason = "FAILURE_REASON";

        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.Failed, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: failureReason, MigrationLogUrl: MIGRATION_URL));
        _mockGithubApi.Setup(x => x.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((MIGRATION_URL, REPO_MIGRATION_ID));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Waiting for migration of repository {TARGET_REPO} to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} failed for {TARGET_REPO}",
                $"Migration log available at {MIGRATION_URL} or by running `gh {TestHelpers.CLI_ROOT_COMMAND} download-logs`"
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage(failureReason);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Migration_ID_That_Fails_Validation()
    {
        // Arrange
        const string failureReason = "FAILURE_REASON";

        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: null, MigrationLogUrl: MIGRATION_URL))
            .Returns((State: RepositoryMigrationStatus.FailedValidation, RepositoryName: TARGET_REPO, WarningsCount: 0, FailureReason: failureReason, MigrationLogUrl: MIGRATION_URL));
        _mockGithubApi.Setup(x => x.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((MIGRATION_URL, REPO_MIGRATION_ID));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Waiting for migration of repository {TARGET_REPO} to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} failed for {TARGET_REPO}",
                $"Migration log available at {MIGRATION_URL} or by running `gh {TestHelpers.CLI_ROOT_COMMAND} download-logs`"
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage(failureReason);

        // Assert

        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Org_Migration_ID_That_Succeeds()
    {
        // Arrange
        const string sourceOrgUrl = "some_url";
        const string targetOrgName = "TARGET_ORG";
        _mockGithubApi.SetupSequence(x => x.GetOrganizationMigration(ORG_MIGRATION_ID).Result)
            .Returns((State: OrganizationMigrationStatus.InProgress, sourceOrgUrl, targetOrgName, FailureReason: null, 0, 0))
            .Returns((State: OrganizationMigrationStatus.RepoMigration, sourceOrgUrl, targetOrgName, FailureReason: null, 1, 1))
            .Returns((State: OrganizationMigrationStatus.Succeeded, sourceOrgUrl, targetOrgName, FailureReason: null, 0, 1));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for {sourceOrgUrl} -> {targetOrgName} migration (ID: {ORG_MIGRATION_ID}) to finish...",
                $"Migration {ORG_MIGRATION_ID} is {OrganizationMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} is {OrganizationMigrationStatus.RepoMigration} - 0/1 repositories completed",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} succeeded"
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = ORG_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetOrganizationMigration(ORG_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Org_Migration_ID_That_Fails()
    {
        // Arrange
        const string failureReason = "Failure Reason";
        const string sourceOrgUrl = "some_url";
        const string targetOrgName = "TARGET_ORG";
        _mockGithubApi.SetupSequence(x => x.GetOrganizationMigration(ORG_MIGRATION_ID).Result)
            .Returns((State: OrganizationMigrationStatus.InProgress, sourceOrgUrl, targetOrgName, FailureReason: null, 0, 0))
            .Returns((State: OrganizationMigrationStatus.RepoMigration, sourceOrgUrl, targetOrgName, FailureReason: null, 1, 1))
            .Returns((State: OrganizationMigrationStatus.Failed, sourceOrgUrl, targetOrgName, failureReason, 0, 1));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for {sourceOrgUrl} -> {targetOrgName} migration (ID: {ORG_MIGRATION_ID}) to finish...",
                $"Migration {ORG_MIGRATION_ID} is {OrganizationMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} is {OrganizationMigrationStatus.RepoMigration} - 0/1 repositories completed",
                $"Waiting {WAIT_INTERVAL} seconds..."
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = ORG_MIGRATION_ID,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"Migration {ORG_MIGRATION_ID} failed for {sourceOrgUrl} -> {targetOrgName}. Failure reason: {failureReason}");

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));

        _mockGithubApi.Verify(m => m.GetOrganizationMigration(ORG_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockGithubApi.VerifyNoOtherCalls();
    }
}

