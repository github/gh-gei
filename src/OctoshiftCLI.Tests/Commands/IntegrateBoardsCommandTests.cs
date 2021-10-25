using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new IntegrateBoardsCommand();
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repos", true);
        }
    }
}
