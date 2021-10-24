using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class CreateTeamTests
    {
        [Fact]
        public async Task CreateTeamWithoutIdp_ShouldMigrate()
        {
            // Arrange
            var targetTeam = Helpers.GetTargetName("no-idp-team");

            var parameterString = $"create-team --github-org {Helpers.TargetOrg} --team-name {targetTeam}";
            var parameters = parameterString.Trim().Split(' ');

            // Act
            await OctoshiftCLI.Program.Main(parameters);

            // Assert
            var exists = await Helpers.TeamExists(Helpers.TargetOrg, targetTeam);
            Assert.True(exists);

            //TODO: Assert membership count

            // Cleanup
            await Helpers.DeleteTeam(Helpers.TargetOrg, targetTeam);
            exists = await Helpers.TeamExists(Helpers.TargetOrg, targetTeam);
            Assert.False(exists, "Failed to cleanup test team");
        }

        [Fact]
        public async Task CreateTeamWithIdp_ShouldMigrate()
        {
            // Arrange
            var targetTeam = Helpers.GetTargetName("with-idp-team");
            var targetIdp = Helpers.GetTargetName("idp-group");

            var parameterString = $"create-team --github-org {Helpers.TargetOrg} --team-name {targetTeam} --idp-group {targetIdp}";
            var parameters = parameterString.Trim().Split(' ');

            // Act
            await OctoshiftCLI.Program.Main(parameters);

            // Assert
            var exists = await Helpers.TeamExists(Helpers.TargetOrg, targetTeam);
            Assert.True(exists);

            //TODO: Assert membership count == 0

            // Cleanup
            await Helpers.DeleteTeam(Helpers.TargetOrg, targetTeam);
            exists = await Helpers.TeamExists(Helpers.TargetOrg, targetTeam);
            Assert.False(exists, "Failed to cleanup test team");
        }
    }
}
