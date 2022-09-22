using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class WaitForMigrationCommandBaseTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly WaitForMigrationCommandBase _command;

    private const string REPO_MIGRATION_ID = "RM_MIGRATION_ID";
    private const string ORG_MIGRATION_ID = "OM_MIGRATION_ID";
    private const string TARGET_REPO = "TARGET_REPO";
    private const int WAIT_INTERVAL = 1;

    public WaitForMigrationCommandBaseTests()
    {
        _command = new WaitForMigrationCommandBase(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object)
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

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

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
        await _command.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockOctoLogger.VerifyNoOtherCalls();
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

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

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
            .Invoking(async () => await _command.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage(failureReason);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockOctoLogger.VerifyNoOtherCalls();
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

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

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
            .Invoking(async () => await _command.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage(failureReason);

        // Assert

        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
        _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetMigration(REPO_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockOctoLogger.VerifyNoOtherCalls();
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Org_Migration_ID_That_Succeeds()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetOrganizationMigrationState(ORG_MIGRATION_ID).Result)
            .Returns(OrganizationMigrationStatus.InProgress)
            .Returns(OrganizationMigrationStatus.InProgress)
            .Returns(OrganizationMigrationStatus.Succeeded);

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for org migration (ID: {ORG_MIGRATION_ID}) to finish...",
                $"Migration {ORG_MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} succeeded"
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = ORG_MIGRATION_ID,
        };
        await _command.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
        _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

        _mockGithubApi.Verify(m => m.GetOrganizationMigrationState(ORG_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockOctoLogger.VerifyNoOtherCalls();
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Org_Migration_ID_That_Fails()
    {
        // Arrange
        _mockGithubApi.SetupSequence(x => x.GetOrganizationMigrationState(ORG_MIGRATION_ID).Result)
            .Returns(OrganizationMigrationStatus.InProgress)
            .Returns(OrganizationMigrationStatus.InProgress)
            .Returns(OrganizationMigrationStatus.Failed);

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var expectedLogOutput = new List<string>
            {
                $"Waiting for org migration (ID: {ORG_MIGRATION_ID}) to finish...",
                $"Migration {ORG_MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {ORG_MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds..."
            };

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = ORG_MIGRATION_ID,
        };
        await FluentActions
            .Invoking(async () => await _command.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"Migration {ORG_MIGRATION_ID} failed");

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));

        _mockGithubApi.Verify(m => m.GetOrganizationMigrationState(ORG_MIGRATION_ID), Times.Exactly(3));

        actualLogOutput.Should().Equal(expectedLogOutput);

        _mockOctoLogger.VerifyNoOtherCalls();
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Invalid_Migration_ID_Prefix_Throws_Exception()
    {
        // Arrange
        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var invalidId = "SomeId";

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = invalidId,
        };
        await FluentActions
            .Invoking(async () => await _command.Handle(args))
            .Should()
            .ThrowAsync<OctoshiftCliException>()
            .WithMessage($"Invalid migration id: {invalidId}");

        // Assert
        _mockOctoLogger.VerifyNoOtherCalls();
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task It_Uses_Github_Pat_When_Provided()
    {
        // Arrange
        const string githubPat = "github-pat";

        _mockGithubApi
            .Setup(x => x.GetMigration(REPO_MIGRATION_ID).Result)
            .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, FailureReason: null));

        _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

        // Act
        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
            GithubPat = githubPat
        };
        await _command.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("GITHUB PAT: ***"));
        _mockTargetGithubApiFactory.Verify(m => m.Create(null, githubPat));
    }
}

