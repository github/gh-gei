using System;
using System.Diagnostics;
using OctoshiftCLI;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class CreateTeamTests
    {
        private GithubClient _client;
        public CreateTeamTests()
        {
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            _client = new GithubClient(githubToken);
        }

        private async Task<bool> TeamExists(string orgName, string teamName)
        {
            try
            {
                var url = $"https://api.github.com/orgs/{orgName}/teams/{teamName}";
                var repo = await _client.GetAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }
        
        private async Task<bool> DeleteTargetTeam(string orgName, string teamName)
        {
            try
            {
                var url = $"https://api.github.com/orgs/{orgName}/teams/{teamName}";
                await _client.DeleteAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }

        [Fact]
        public async Task WithTeamNotIdp_ShouldMigrate()
        {
            await this.DeleteTargetTeam("GuacamoleResearch", "int-git-Maintainers");
            // Verify the team doesn't exist before creating
            var exists = await this.TeamExists("GuacamoleResearch", "int-git-Maintainers");
            Assert.False(exists, "Unable to delete team as part of 'Arrange'");

            var parameterString = "create-team --github-org GuacamoleResearch --team-name int-git-Maintainers";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            exists = await this.TeamExists("GuacamoleResearch", "int-git-Maintainers");
            Assert.True(exists);

            //TODO: Assert membership count
        }

        [Fact]
        public async Task WithTeamAndIdp_ShouldMigrate()
        {
            await this.DeleteTargetTeam("GuacamoleResearch", "int-git-Admins");
            // Verify the team doesn't exist before creating
            var exists = await this.TeamExists("GuacamoleResearch", "int-git-Admins");
            Assert.False(exists, "Unable to delete team as part of 'Arrange'");

            var parameterString = "create-team --github-org GuacamoleResearch --team-name int-git-Admins --idp-group int-git-Admins";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            exists = await this.TeamExists("GuacamoleResearch", "int-git-Admins");
            Assert.True(exists);

            //TODO: Assert membership count == 0
        }


    }
}
