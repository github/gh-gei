using System;
using System.Diagnostics;
using OctoshiftCLI;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class MigrateRepoTests: IntegrationTestBase
    {
        protected override string GitHubApiUrl()
        {
            return "https://api.github.com/repos/{orgName}/{name}";
        }

        [Fact]
        public async Task WithEmptyRepo_ShouldMigrate()
        {
            await this.Delete("GuacamoleResearch", "git-empty");

            var parameterString = "migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"git-empty\" --github-org \"GuacamoleResearch\" --github-repo \"git-empty\"";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            var exists = await this.Exists("GuacamoleResearch", "git-empty");
            Assert.False(exists);
        }

        [Fact]
        public async Task WithPopoulatedRepo_ShouldIncludeHistory()
        {
            await this.Delete("GuacamoleResearch", "int-git1");

            var parameterString = "migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"int-git1\" --github-org \"GuacamoleResearch\" --github-repo \"int-git1\"";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            var exists = await this.Exists("GuacamoleResearch", "int-git1");
            Assert.False(exists);

            //TODO: Add verification of file history
        }
    }
}
