using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new MigrateRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("migrate-repo", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
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
            var githubToken = Guid.NewGuid().ToString();
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(string.Empty);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateMigrationSource(githubOrgId, adoToken).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            Environment.SetEnvironmentVariable("GH_PAT", githubToken);
            GithubApiFactory.Create = token => token == githubToken ? mockGithub.Object : null;

            var command = new MigrateRepoCommand();
            await command.Invoke(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task MissingGithubPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("ADO_PAT", string.Empty);

            var command = new MigrateRepoCommand();

            await command.Invoke("foo", "foo", "foo", "foo", "foo");
        }
    }
}
