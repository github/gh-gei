using System;
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
            command.Options.Count.Should().Be(7);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, adoToken, githubPat, false).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    adoRepoUrl,
                    githubOrgId,
                    githubRepo,
                    adoToken,
                    githubPat,
                    "",
                    "").Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task With_Ssh()
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, adoToken, githubPat, true).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                        migrationSourceId,
                        adoRepoUrl,
                        githubOrgId,
                        githubRepo,
                        adoToken,
                        null,
                        "",
                        "").Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Retries_When_Hosts_Error()
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, adoToken, githubPat, false).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                        migrationSourceId,
                        adoRepoUrl,
                        githubOrgId,
                        githubRepo,
                        adoToken,
                        githubPat,
                        "",
                        "").Result)
                .Returns(migrationId);
            mockGithub.SetupSequence(x => x.GetMigrationState(migrationId).Result).Returns("FAILED").Returns("SUCCEEDED");
            mockGithub.Setup(x => x.GetMigrationFailureReason(migrationId).Result).Returns("Warning: Permanently added XXXXX (ECDSA) to the list of known hosts");

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo);

            mockGithub.Verify(x => x.DeleteRepo(githubOrg, githubRepo));
            mockGithub.Verify(
                x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo, adoToken, githubPat, "", ""), Times.Exactly(2));
        }
    }
}
