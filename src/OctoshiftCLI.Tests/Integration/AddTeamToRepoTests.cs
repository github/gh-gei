using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class AddTeamToRepoTests
    {
        [Fact]
        public async Task ShouldAddTeam()
        {
            // Arrange - create team before adding
            var targetTeam = TestHelpers.GetTargetName("team");
            var parameterString = $"create-team --github-org {TestHelpers.TargetOrg} --team-name {targetTeam}";
            var parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            var exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.True(exists, "Failed to create team (arrange) before add test");

            // Act - add team to repo
            parameterString = $"add-team-to-repo --github-org {TestHelpers.TargetOrg} --github-repo tmp --team CreateAddTeam --role maintainer";
            parameters = parameterString.Trim().Split(' ');
            await OctoshiftCLI.Program.Main(parameters);

            // Assert
            //TODO: Verify team is a maintainer in the repo

            // Cleanup
            await TestHelpers.DeleteTeam(TestHelpers.TargetOrg, targetTeam);
            exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.False(exists, "Unable to delete team as part of cleanup");
        }
    }
}
