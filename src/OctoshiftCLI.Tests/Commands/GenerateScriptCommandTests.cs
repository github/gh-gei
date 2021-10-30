using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new GenerateScriptCommand();
            Assert.NotNull(command);
            Assert.Equal("generate-script", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "repos-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-idp", false);
        }
    }
}