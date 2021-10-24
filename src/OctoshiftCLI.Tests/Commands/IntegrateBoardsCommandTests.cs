using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new IntegrateBoardsCommand();
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(4, command.Options.Count);

            // TODO: This looks wrong
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);



            Assert.Equal("ado-org", command.Options[0].Name);
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal("ado-team-project", command.Options[1].Name);
            Assert.True(command.Options[1].IsRequired);
            Assert.Equal("github-org", command.Options[2].Name);
            Assert.True(command.Options[2].IsRequired);
            Assert.Equal("github-repos", command.Options[3].Name);
            Assert.True(command.Options[3].IsRequired);
        }
    }
}
