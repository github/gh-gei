using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class AddTeamToRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new AddTeamToRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "team", true);
            TestHelpers.VerifyCommandOption(command.Options, "role", true);
        }
    }
}
