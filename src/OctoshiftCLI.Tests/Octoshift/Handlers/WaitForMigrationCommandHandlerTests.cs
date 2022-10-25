using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class WaitForMigrationCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly WaitForMigrationCommandHandler _handler;

    private const string REPO_MIGRATION_ID = "RM_MIGRATION_ID";
    private const string ORG_MIGRATION_ID = "OM_MIGRATION_ID";
    private const string TARGET_REPO = "TARGET_REPO";
    private const int WAIT_INTERVAL = 0;

    public WaitForMigrationCommandHandlerTests()
    {
        _handler = new WaitForMigrationCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object)
        {
            WaitIntervalInSeconds = WAIT_INTERVAL
        };
    }

    [Fact]
    public async Task With_Migration_ID_That_Succeeds()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, FailureReason: null));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} succeeded for {TARGET_REPO}"
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
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
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.Failed, RepositoryName: TARGET_REPO, FailureReason: failureReason));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} failed for {TARGET_REPO}"
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
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
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
            .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, FailureReason: null))
            .Returns((State: RepositoryMigrationStatus.FailedValidation, RepositoryName: TARGET_REPO, FailureReason: failureReason));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {REPO_MIGRATION_ID}) to finish...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {REPO_MIGRATION_ID} failed for {TARGET_REPO}"
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

        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
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

    [Fact]
    public async Task With_Invalid_Migration_ID_Prefix_Throws_Exception()
    {
        // Arrange
        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var invalidId = "SomeId";

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = invalidId,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"Invalid migration id: {invalidId}");

        // Assert
        _mockGithubApi.VerifyNoOtherCalls();
    }
}

