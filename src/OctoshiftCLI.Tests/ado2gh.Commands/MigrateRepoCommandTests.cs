using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.ado2gh.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.ado2gh.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
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

            var mockGithub = new Mock<GithubApi>(null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, adoToken, githubPat).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<OctoshiftCLI.ado2gh.EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);
            environmentVariableProviderMock
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(adoToken);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, new Lazy<GithubApi>(mockGithub.Object),
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }
    }
}