using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class WaitForMigrationTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new WaitForMigrationCommand(null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("wait-for-migration");
            command.Options.Count.Should().Be(3);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "migration-id", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task With_Migration_ID_That_Succeeds()
        {
            // Arrange
            const string githubOrg = "GITHUB_ORG";
            const string specifiedMigrationId = "MIGRATION_ID";
            const string githubOrgId = "GITHUB_ORG_ID";

            const string ongoingMigrationId1 = "ONGOING_MIGRATION_ID_1";
            const string ongoingMigrationId2 = "ONGOING_MIGRATION_ID_2";
            const string ongoingMigrationId3 = "ONGOING_MIGRATION_ID_3";

            const string previouslyFailedMigration = "PREVIOUS_FAILED_MIGRATION";
            const string previouslySucceededMigration = "PREVIOUS_SUCCEEDED_MIGRATION";

            var mockGithubApi = new Mock<GithubApi>(null, null);
            mockGithubApi.Setup(m => m.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.SetupSequence(x => x.GetMigrationState(specifiedMigrationId).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Succeeded);
            mockGithubApi.SetupSequence(m => m.GetMigrationStates(githubOrgId).Result)
                .Returns(new[]
                {
                    (MigrationId: specifiedMigrationId, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.Queued),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                })
                .Returns(new[]
                {
                    (MigrationId: specifiedMigrationId, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                });

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithubApi.Object);

            var mockLogger = new Mock<OctoLogger>();

            // Act
            const int waitIntervalInSeconds = 1;
            var command = new WaitForMigrationCommand(mockLogger.Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await command.Invoke(githubOrg, specifiedMigrationId);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(githubOrg));

            mockLogger.Verify(
                m => m.LogInformation($"Waiting for migration {specifiedMigrationId} to finish..."),
                Times.Once);
            mockLogger.Verify(m => m.LogInformation($"GITHUB ORG: {githubOrg}"), Times.Once);
            mockLogger.Verify(m => m.LogInformation($"MIGRATION ID: {specifiedMigrationId}"), Times.Once);

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId), Times.Exactly(3));
            mockLogger.Verify(
                m => m.LogInformation($"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}"),
                Times.Exactly(2));
            mockLogger.Verify(
                m => m.LogSuccess($"Migration succeeded for migration {specifiedMigrationId}"),
                Times.Once);

            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId), Times.Exactly(2));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 3, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 1"));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 4, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 0"));
            
            mockLogger.Verify(m => m.LogInformation($"Waiting {waitIntervalInSeconds} seconds..."), Times.Exactly(2));

            mockLogger.VerifyNoOtherCalls();
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Migration_ID_That_Fails()
        {
            // Arrange
            const string githubOrg = "GITHUB_ORG";
            const string specifiedMigrationId = "MIGRATION_ID";
            const string githubOrgId = "GITHUB_ORG_ID";

            const string ongoingMigrationId1 = "ONGOING_MIGRATION_ID_1";
            const string ongoingMigrationId2 = "ONGOING_MIGRATION_ID_2";
            const string ongoingMigrationId3 = "ONGOING_MIGRATION_ID_3";

            const string previouslyFailedMigration = "PREVIOUS_FAILED_MIGRATION";
            const string previouslySucceededMigration = "PREVIOUS_SUCCEEDED_MIGRATION";

            const string failureReason = "FAILURE_REASON";

            var mockGithubApi = new Mock<GithubApi>(null, null);
            mockGithubApi.Setup(m => m.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.Setup(m => m.GetMigrationFailureReason(specifiedMigrationId).Result).Returns(failureReason);
            mockGithubApi.SetupSequence(x => x.GetMigrationState(specifiedMigrationId).Result)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.InProgress)
                .Returns(RepositoryMigrationStatus.Failed);
            mockGithubApi.SetupSequence(m => m.GetMigrationStates(githubOrgId).Result)
                .Returns(new[]
                {
                    (MigrationId: specifiedMigrationId, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.Queued),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                })
                .Returns(new[]
                {
                    (MigrationId: specifiedMigrationId, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                });

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithubApi.Object);

            var mockLogger = new Mock<OctoLogger>();

            // Act
            const int waitIntervalInSeconds = 1;
            var command = new WaitForMigrationCommand(mockLogger.Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, specifiedMigrationId))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage(failureReason);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(githubOrg));

            mockLogger.Verify(m => m.LogInformation($"Waiting for migration {specifiedMigrationId} to finish..."));
            mockLogger.Verify(m => m.LogInformation($"GITHUB ORG: {githubOrg}"));
            mockLogger.Verify(m => m.LogInformation($"MIGRATION ID: {specifiedMigrationId}"));

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId));
            mockLogger.Verify(m =>
                m.LogInformation($"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}"));
            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 3, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 1"));
            mockLogger.Verify(m => m.LogInformation($"Waiting {waitIntervalInSeconds} seconds..."));

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId));
            mockLogger.Verify(m =>
                m.LogInformation($"Migration {specifiedMigrationId} is {RepositoryMigrationStatus.InProgress}"));
            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 4, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 0"));
            mockLogger.Verify(m => m.LogInformation($"Waiting {waitIntervalInSeconds} seconds..."));

            mockGithubApi.Verify(m => m.GetMigrationState(specifiedMigrationId));
            mockGithubApi.Verify(m => m.GetMigrationFailureReason(specifiedMigrationId));
            mockLogger.Verify(m => m.LogError($"Migration failed for migration {specifiedMigrationId}"));

            mockLogger.VerifyNoOtherCalls();
            mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Without_Migration_ID()
        {
            // Arrange
            const string githubOrg = "GITHUB_ORG";
            const string githubOrgId = "GITHUB_ORG_ID";

            const string ongoingMigrationId1 = "ONGOING_MIGRATION_ID_1";
            const string ongoingMigrationId2 = "ONGOING_MIGRATION_ID_2";
            const string ongoingMigrationId3 = "ONGOING_MIGRATION_ID_3";

            const string previouslyFailedMigration = "PREVIOUS_FAILED_MIGRATION";
            const string previouslySucceededMigration = "PREVIOUS_SUCCEEDED_MIGRATION";

            var mockGithubApi = new Mock<GithubApi>(null, null);
            mockGithubApi.Setup(m => m.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithubApi.SetupSequence(m => m.GetMigrationStates(githubOrgId).Result)
                .Returns(new[]
                {
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.Queued),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                })
                .Returns(new[]
                {
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.InProgress),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                })
                .Returns(new[]
                {
                    (MigrationId: ongoingMigrationId1, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: ongoingMigrationId2, State: RepositoryMigrationStatus.Succeeded),
                    (MigrationId: ongoingMigrationId3, State: RepositoryMigrationStatus.Succeeded),
                    (MigrationId: previouslyFailedMigration, State: RepositoryMigrationStatus.Failed),
                    (MigrationId: previouslySucceededMigration, State: RepositoryMigrationStatus.Succeeded)
                });

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithubApi.Object);

            var mockLogger = new Mock<OctoLogger>();

            // Act
            const int waitIntervalInSeconds = 1;
            var command = new WaitForMigrationCommand(mockLogger.Object, mockGithubApiFactory.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };
            await command.Invoke(githubOrg);

            // Assert
            mockGithubApi.Verify(m => m.GetOrganizationId(githubOrg));

            mockLogger.Verify(m => m.LogInformation($"Waiting for all migrations to finish..."));
            mockLogger.Verify(m => m.LogInformation($"GITHUB ORG: {githubOrg}"));

            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 2, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 1"));
            mockLogger.Verify(m => m.LogInformation($"Waiting {waitIntervalInSeconds} seconds..."));

            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 3, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 0"));
            mockLogger.Verify(m => m.LogInformation($"Waiting {waitIntervalInSeconds} seconds..."));

            mockGithubApi.Verify(m => m.GetMigrationStates(githubOrgId));
            mockLogger.Verify(m => m.LogInformation($"Total migrations {RepositoryMigrationStatus.InProgress}: 0, " +
                                                    $"Total migrations {RepositoryMigrationStatus.Queued}: 0"));

            mockLogger.VerifyNoOtherCalls();
            mockGithubApi.VerifyNoOtherCalls();
        }
    }
}
