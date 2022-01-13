using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand(null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(6);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "source-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubSourceOrg = "foo-source-org";
            var sourceRepo = "foo-repo-source";
            var githubTargetOrg = "foo-target-org";
            var targetRepo = "foo-target-repo";

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var githubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{githubSourceOrg}/{sourceRepo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null);
            mockGithub.Setup(x => x.GetOrganizationId(githubTargetOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, githubPat).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, targetRepo).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<OctoshiftCLI.GithubEnterpriseImporter.EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, new Lazy<GithubApi>(mockGithub.Object), environmentVariableProviderMock.Object);
            await command.Invoke(githubSourceOrg, sourceRepo, githubTargetOrg, targetRepo);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task With_Ssh()
        {
            var githubSourceOrg = "foo-source-org";
            var sourceRepo = "foo-repo-source";
            var githubTargetOrg = "foo-target-org";
            var targetRepo = "foo-target-repo";

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var githubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{githubSourceOrg}/{sourceRepo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null);
            mockGithub.Setup(x => x.GetOrganizationId(githubTargetOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSourceSsh(githubOrgId, githubPat).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, targetRepo).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<OctoshiftCLI.GithubEnterpriseImporter.EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, new Lazy<GithubApi>(mockGithub.Object), environmentVariableProviderMock.Object);
            await command.Invoke(githubSourceOrg, sourceRepo, githubTargetOrg, targetRepo, true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }
    }
}