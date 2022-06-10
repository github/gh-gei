using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class WaitForMigrationCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly WaitForMigrationCommand _command;

        private const string MIGRATION_ID = "MIGRATION_ID";
        private const string TARGET_REPO = "TARGET_REPO";
        private const int WAIT_INTERVAL = 1;

        public WaitForMigrationCommandTests()
        {
            _command = new WaitForMigrationCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = WAIT_INTERVAL
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("wait-for-migration");
            _command.Options.Count.Should().Be(3);

            TestHelpers.VerifyCommandOption(_command.Options, "migration-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task With_Migration_ID_That_Succeeds()
        {
            // Arrange
            _mockGithubApi.SetupSequence(x => x.GetMigration(MIGRATION_ID).Result)
                .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, FailureReason: null));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {MIGRATION_ID}) to finish...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} succeeded for {TARGET_REPO}"
            };

            // Act
            await _command.Invoke(MIGRATION_ID);

            // Assert
            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

            _mockGithubApi.Verify(m => m.GetMigration(MIGRATION_ID), Times.Exactly(3));

            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockOctoLogger.VerifyNoOtherCalls();
            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Migration_ID_That_Fails()
        {
            // Arrange
            const string failureReason = "FAILURE_REASON";

            _mockGithubApi.SetupSequence(x => x.GetMigration(MIGRATION_ID).Result)
                .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.InProgress, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.Failed, RepositoryName: TARGET_REPO, FailureReason: failureReason));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {MIGRATION_ID}) to finish...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} failed for {TARGET_REPO}"
            };

            // Act
            await FluentActions
                .Invoking(async () => await _command.Invoke(MIGRATION_ID))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage(failureReason);

            // Assert
            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

            _mockGithubApi.Verify(m => m.GetMigration(MIGRATION_ID), Times.Exactly(3));

            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockOctoLogger.VerifyNoOtherCalls();
            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Migration_ID_That_Fails_Validation()
        {
            // Arrange
            const string failureReason = "FAILURE_REASON";

            _mockGithubApi.SetupSequence(x => x.GetMigration(MIGRATION_ID).Result)
                .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.PendingValidation, RepositoryName: TARGET_REPO, FailureReason: null))
                .Returns((State: RepositoryMigrationStatus.FailedValidation, RepositoryName: TARGET_REPO, FailureReason: failureReason));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for {TARGET_REPO} migration (ID: {MIGRATION_ID}) to finish...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} for {TARGET_REPO} is {RepositoryMigrationStatus.PendingValidation}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} failed for {TARGET_REPO}"
            };

            // Act
            await FluentActions
                .Invoking(async () => await _command.Invoke(MIGRATION_ID))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage(failureReason);

            // Assert

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            _mockOctoLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

            _mockGithubApi.Verify(m => m.GetMigration(MIGRATION_ID), Times.Exactly(3));

            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockOctoLogger.VerifyNoOtherCalls();
            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            // Arrange
            const string githubPat = "github-pat";

            _mockGithubApi
                .Setup(x => x.GetMigration(MIGRATION_ID).Result)
                .Returns((State: RepositoryMigrationStatus.Succeeded, RepositoryName: TARGET_REPO, FailureReason: null));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            // Act
            await _command.Invoke(MIGRATION_ID, githubPat);

            // Assert
            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }
    }
}
