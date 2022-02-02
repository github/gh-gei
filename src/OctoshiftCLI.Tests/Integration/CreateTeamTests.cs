using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public class CreateTeamTests
    {
        [Fact]
        public async Task Should_Migrate_Without_Idp()
        {
            // Arrange
            var targetTeam = TestHelpers.GetTargetName("no-idp-team");

            var parameterString = $"create-team --github-org {TestHelpers.TargetOrg} --team-name {targetTeam}";
            var parameters = parameterString.Trim().Split(' ');

            // Act
            await OctoshiftCLI.AdoToGithub.Program.Main(parameters);

            // Assert
            var exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.True(exists);

            //TODO: Assert membership count

            // Cleanup
            await TestHelpers.DeleteTeam(TestHelpers.TargetOrg, targetTeam);
            exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.False(exists, "Failed to cleanup test team");
        }

        [Fact]
        public async Task Should_Migrate_With_Idp()
        {
            // Arrange
            var targetTeam = TestHelpers.GetTargetName("with-idp-team");
            var targetIdp = TestHelpers.GetTargetName("idp-group");

            var parameterString = $"create-team --github-org {TestHelpers.TargetOrg} --team-name {targetTeam} --idp-group {targetIdp}";
            var parameters = parameterString.Trim().Split(' ');

            // Act
            await OctoshiftCLI.AdoToGithub.Program.Main(parameters);

            // Assert
            var exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.True(exists);

            //TODO: Assert membership count == 0

            // Cleanup
            await TestHelpers.DeleteTeam(TestHelpers.TargetOrg, targetTeam);
            exists = await TestHelpers.TeamExists(TestHelpers.TargetOrg, targetTeam);
            Assert.False(exists, "Failed to cleanup test team");
        }
    }
}
