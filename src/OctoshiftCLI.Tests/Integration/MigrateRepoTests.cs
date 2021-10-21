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
        private GithubClient _client;
        public MigrateRepoTests()
        {
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            _client = new GithubClient(githubToken);
        }

        private async Task<bool> RepoExists(string orgName, string repoName)
        {
            try
            {
                var url = $"https://api.github.com/repos/{orgName}/{repoName}";
                var repo = await _client.GetAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }
        
        private async Task<bool> DeleteTargetRepo(string orgName, string repoName)
        {
            // REFERENCE: https://docs.github.com/en/rest/reference/repos#delete-a-repository
            // Doesn't seem achievable via GraphQL mutations
            try
            {
                var url = $"https://api.github.com/repos/{orgName}/{repoName}";
                await _client.DeleteAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }

        [Fact]
        public async Task WithEmptyRepo_ShouldMigrate()
        {
            await this.DeleteTargetRepo("GuacamoleResearch", "git-empty");

            var parameterString = "migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"git-empty\" --github-org \"GuacamoleResearch\" --github-repo \"git-empty\"";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            var exists = await this.RepoExists("GuacamoleResearch", "git-empty");
            Assert.False(exists);
        }

        [Fact]
        public async Task WithPopoulatedRepo_ShouldIncludeHistory()
        {
            await this.DeleteTargetRepo("GuacamoleResearch", "int-git1");

            var parameterString = "migrate-repo --ado-org \"OCLI\" --ado-team-project \"int-git\" --ado-repo \"int-git1\" --github-org \"GuacamoleResearch\" --github-repo \"int-git1\"";
            var parameters = parameterString.Trim().Split(' ');
            //TODO: Perform the migration (uncomment below) then reverse polarity of the test
            // need Octoshift enabled on GuacamoleResearch before continuine (or move to a different GH org)
            // await OctoshiftCLI.Program.Main(parameters);

            var exists = await this.RepoExists("GuacamoleResearch", "int-git1");
            Assert.False(exists);

            //TODO: Add verification of file history
        }

    }
}
