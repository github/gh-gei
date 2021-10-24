using System;
using System.Diagnostics;
using OctoshiftCLI;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class MigrateRepoTests
    {
        [Fact]
        public async Task WithEmptyRepo_ShouldMigrate()
        {
            // Arrange
            var targetRepo = Helpers.TargetName("empty-repo");

            var parameterString = $"migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"git-empty\" --github-org {Helpers.TargetOrg()} --github-repo {targetRepo}";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            // Act
            var exists = await Helpers.RepoExists(Helpers.TargetOrg(), targetRepo);

            // Assert
            Assert.False(exists);

            // Cleanup
            await Helpers.DeleteRepo(Helpers.TargetOrg(), targetRepo);
        }

        [Fact]
        public async Task WithPopoulatedRepo_ShouldIncludeHistory()
        {
            // Arrange
            var targetRepo = Helpers.TargetName("repo");

            // Arrange
            var parameterString = $"migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"int-git1\" --github-org {Helpers.TargetOrg()} --github-repo {targetRepo}";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            // Act
            var exists = await Helpers.RepoExists(Helpers.TargetOrg(), "int-git1");

            // Assert
            Assert.False(exists);

            //TODO: Add verification of file history

            // Cleanup
            await Helpers.DeleteRepo(Helpers.TargetOrg(), targetRepo);
        }
    }
}
