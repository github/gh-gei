using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class WaitForMigrationCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly WaitForMigrationCommand _command;

        private const string MIGRATION_ID = "MIGRATION_ID";
        private const int WAIT_INTERVAL = 1;

        public WaitForMigrationCommandTests()
        {
            _command = new WaitForMigrationCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object)
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
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task With_Migration_ID_That_Succeeds()
        {
            // Arrange
            _mockGithubApi.SetupSequence(x => x.GetMigrationState(MIGRATION_ID).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Succeeded);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for migration {MIGRATION_ID} to finish...",
                $"Migration {MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration succeeded for migration {MIGRATION_ID}"
            };

            // Act
            await _command.Invoke(MIGRATION_ID);

            // Assert
            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

            _mockGithubApi.Verify(m => m.GetMigrationState(MIGRATION_ID), Times.Exactly(3));

            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockOctoLogger.VerifyNoOtherCalls();
            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Migration_ID_That_Fails()
        {
            // Arrange
            const string failureReason = "FAILURE_REASON";

            _mockGithubApi.Setup(m => m.GetMigrationFailureReason(MIGRATION_ID).Result).Returns(failureReason);
            _mockGithubApi.SetupSequence(x => x.GetMigrationState(MIGRATION_ID).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Failed);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for migration {MIGRATION_ID} to finish...",
                $"Migration {MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration {MIGRATION_ID} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {WAIT_INTERVAL} seconds...",
                $"Migration failed for migration {MIGRATION_ID}"
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

            _mockGithubApi.Verify(m => m.GetMigrationState(MIGRATION_ID), Times.Exactly(3));
            _mockGithubApi.Verify(m => m.GetMigrationFailureReason(MIGRATION_ID), Times.Once);

            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockOctoLogger.VerifyNoOtherCalls();
            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Uses_Github_Target_Pat_When_Provided()
        {
            // Arrange
            const string githubTargetPat = "github-target-pat";

            _mockGithubApi
                .Setup(x => x.GetMigrationState(MIGRATION_ID).Result)
                .Returns(RepositoryMigrationStatus.Succeeded);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubTargetPat)).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            await _command.Invoke(MIGRATION_ID, githubTargetPat);

            // Assert
            actualLogOutput.Should().Contain("GITHUB TARGET PAT: ***");
            _mockTargetGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
        }
    }
}
