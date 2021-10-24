using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class RewirePipelineCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new RewirePipelineCommand();
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
        }
    }
}
