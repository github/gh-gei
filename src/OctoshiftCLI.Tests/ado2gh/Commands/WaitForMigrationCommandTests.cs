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
        [Fact]
        public void Should_Have_Options()
        {
            var command = new WaitForMigrationCommand(null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("wait-for-migration");
            command.Options.Count.Should().Be(3);

            TestHelpers.VerifyCommandOption(command.Options, "migration-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task With_Migration_ID_That_Succeeds()
        {
            // Arrange
            const string specifiedMigrationId = "MIGRATION_ID";
            const int waitIntervalInSeconds = 1;

            var mockGithubApi = new Mock<GithubApi>(null, null, null);
            mockGithubApi.SetupSequence(x => x.GetMigrationState(specifiedMigrationId).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Succeeded);

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for migration {specifiedMigrationId} to finish...",
                $"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {waitIntervalInSeconds} seconds...",
                $"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {waitIntervalInSeconds} seconds...",
                $"Migration succeeded for migration {specifiedMigrationId}"
            };

            // Act
            var command = new WaitForMigrationCommand(mockLogger.Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await command.Invoke(specifiedMigrationId);

            // Assert
            mockLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            mockLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId), Times.Exactly(3));

            actualLogOutput.Should().Equal(expectedLogOutput);

            mockLogger.VerifyNoOtherCalls();
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Migration_ID_That_Fails()
        {
            // Arrange
            const string specifiedMigrationId = "MIGRATION_ID";

            const string failureReason = "FAILURE_REASON";
            const int waitIntervalInSeconds = 1;

            var mockGithubApi = new Mock<GithubApi>(null, null, null);
            mockGithubApi.Setup(m => m.GetMigrationFailureReason(specifiedMigrationId).Result).Returns(failureReason);
            mockGithubApi.SetupSequence(x => x.GetMigrationState(specifiedMigrationId).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Failed);

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogError(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Waiting for migration {specifiedMigrationId} to finish...",
                $"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {waitIntervalInSeconds} seconds...",
                $"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}",
                $"Waiting {waitIntervalInSeconds} seconds...",
                $"Migration failed for migration {specifiedMigrationId}"
            };

            // Act
            var command = new WaitForMigrationCommand(mockLogger.Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await FluentActions
                .Invoking(async () => await command.Invoke(specifiedMigrationId))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage(failureReason);

            // Assert
            mockLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(5));
            mockLogger.Verify(m => m.LogError(It.IsAny<string>()), Times.Once);

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId), Times.Exactly(3));
            mockGithubApi.Verify(m => m.GetMigrationFailureReason(specifiedMigrationId), Times.Once);

            actualLogOutput.Should().Equal(expectedLogOutput);

            mockLogger.VerifyNoOtherCalls();
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            // Arrange
            const string specifiedMigrationId = "MIGRATION_ID";
            const int waitIntervalInSeconds = 1;
            const string githubPat = "github-pat";

            var mockGithubApi = new Mock<GithubApi>(null, null, null);
            mockGithubApi
                .Setup(x => x.GetMigrationState(specifiedMigrationId).Result)
                .Returns(RepositoryMigrationStatus.Succeeded);

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithubApi.Object);

            // Act
            var command = new WaitForMigrationCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await command.Invoke(specifiedMigrationId, githubPat);

            // Assert
            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }
    }
}
