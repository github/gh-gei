using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("generate-script", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "repos-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-idp", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }
    }
}