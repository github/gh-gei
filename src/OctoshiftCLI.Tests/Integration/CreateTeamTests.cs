using System;
using System.Diagnostics;
using OctoshiftCLI;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class CreateTeamTests : IntegrationTestBase
    {
        protected override string GitHubApiUrl()
        {
            return "https://api.github.com/orgs/{orgName}/teams/{name}";
        }


        [Fact]
        public async Task WithTeamWithoutIdp_ShouldMigrate()
        {
            await this.Delete("GuacamoleResearch", "int-git-Maintainers");
            // Verify the team doesn't exist before creating
            var exists = await this.Exists("GuacamoleResearch", "int-git-Maintainers");
            Assert.False(exists, "Unable to delete team as part of 'Arrange'");

            var parameterString = "create-team --github-org GuacamoleResearch --team-name int-git-Maintainers";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            exists = await this.Exists("GuacamoleResearch", "int-git-Maintainers");
            Assert.True(exists);

            //TODO: Assert membership count
        }

        [Fact]
        public async Task WithTeamAndIdp_ShouldMigrate()
        {
            await this.Delete("GuacamoleResearch", "int-git-Admins");
            // Verify the team doesn't exist before creating
            var exists = await this.Exists("GuacamoleResearch", "int-git-Admins");
            Assert.False(exists, "Unable to delete team as part of 'Arrange'");

            var parameterString = "create-team --github-org GuacamoleResearch --team-name int-git-Admins --idp-group int-git-Admins";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            exists = await this.Exists("GuacamoleResearch", "int-git-Admins");
            Assert.True(exists);

            //TODO: Assert membership count == 0
        }


    }
}
