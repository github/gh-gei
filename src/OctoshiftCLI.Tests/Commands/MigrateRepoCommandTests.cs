using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new MigrateRepoCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("migrate-repo", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
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

            var mockGithub = new Mock<GithubApi>(null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateMigrationSource(githubOrgId, adoToken, githubPat).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var githubFactory = new Mock<GithubApiFactory>(mockGithub.Object);
            githubFactory.Setup(m => m.GetGithubToken()).Returns(githubPat);

            using var adoFactory = new AdoApiFactory(adoToken);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, adoFactory, githubFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }
    }
}