using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class CreateTeamCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new CreateTeamCommand();
            Assert.NotNull(command);
            Assert.Equal("create-team", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "team-name", true);
            TestHelpers.VerifyCommandOption(command.Options, "idp-group", false);
        }
    }
}
