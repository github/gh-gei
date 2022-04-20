using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand(null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(10);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            // Arrange
            const string adoOrg = "FooOrg";
            const string adoTeamProject = "BlahTeamProject";
            const string adoRepo = "foo-repo";
            const string githubOrg = "foo-gh-org";
            const string githubRepo = "gh-repo";
            var adoRepoUrl = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_git/{adoRepo}";
            var adoToken = Guid.NewGuid().ToString();
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var migrationId = Guid.NewGuid().ToString();
            var githubPat = Guid.NewGuid().ToString();

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetRepos(githubOrg).Result).Returns(new List<string>());
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    adoRepoUrl,
                    githubOrgId,
                    githubRepo,
                    adoToken,
                    githubPat,
                    null,
                    null,
                    false).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var actualLogOutput = new List<string>();
            var mockLogger = TestHelpers.CreateMock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                "Migrating Repo...",
                $"ADO ORG: {adoOrg}",
                $"ADO TEAM PROJECT: {adoTeamProject}",
                $"ADO REPO: {adoRepo}",
                $"GITHUB ORG: {githubOrg}",
                $"GITHUB REPO: {githubRepo}",
                $"A repository migration (ID: {migrationId}) was successfully queued."
            };

            // Act
            var command = new MigrateRepoCommand(mockLogger.Object, mockGithubApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, wait: false);

            // Assert
            mockGithub.Verify(m => m.GetRepos(githubOrg));
            mockGithub.Verify(m => m.GetOrganizationId(githubOrg));
            mockGithub.Verify(m => m.CreateAdoMigrationSource(githubOrgId));
            mockGithub.Verify(m => m.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo, adoToken, githubPat, null, null, false));

            mockLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
            actualLogOutput.Should().Equal(expectedLogOutput);

            mockGithub.VerifyNoOtherCalls();
            mockLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Idempotency_Stop_If_Target_Exists()
        {
            // Arrange
            const string adoOrg = "FooOrg";
            const string adoTeamProject = "BlahTeamProject";
            const string adoRepo = "foo-repo";
            const string githubOrg = "foo-gh-org";
            const string githubRepo = "gh-repo";
            var adoToken = Guid.NewGuid().ToString();
            var githubPat = Guid.NewGuid().ToString();
            var githubRepos = new List<string> { githubRepo };

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetRepos(githubOrg).Result).Returns(githubRepos);

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = $"The Org '{githubOrg}' already contains a repository with the name '{githubRepo}'. No operation will be performed";

            // Act
            var command = new MigrateRepoCommand(mockLogger.Object, mockGithubApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, wait: false);

            // Assert
            mockGithub.Verify(m => m.GetRepos(githubOrg));

            mockLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Contain(expectedLogOutput);

            mockGithub.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Happy_Path_With_Wait()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoRepo = "foo-repo";
            var githubOrg = "foo-gh-org";
            var githubRepo = "gh-repo";
            var adoRepoUrl = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_git/{adoRepo}";
            var adoToken = Guid.NewGuid().ToString();
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var migrationId = Guid.NewGuid().ToString();
            var githubPat = Guid.NewGuid().ToString();

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                        migrationSourceId,
                        adoRepoUrl,
                        githubOrgId,
                        githubRepo,
                        adoToken,
                        githubPat,
                        null,
                        null,
                        false).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var command = new MigrateRepoCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task It_Uses_Ado_And_Github_Pats_When_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetMigrationState(It.IsAny<string>()).Result).Returns("SUCCEEDED");
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithub.Object);

            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoPat);

            var command = new MigrateRepoCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "adoRepo", "githubOrg", "githubRepo", wait: true, adoPat: adoPat, githubPat: githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
            environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken(), Times.Never);
            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public async Task It_Falls_Back_To_Ado_And_Github_Pats_From_Environment_When_Not_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetRepos("githubOrg").Result).Returns(new List<string>());
            mockGithub.Setup(x => x.GetMigrationState(It.IsAny<string>()).Result).Returns("SUCCEEDED");
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithub.Object);

            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoPat);

            var command = new MigrateRepoCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "adoRepo", "githubOrg", "githubRepo", wait: true);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
            environmentVariableProviderMock.Verify(m => m.AdoPersonalAccessToken());
            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken());
        }
    }
}
