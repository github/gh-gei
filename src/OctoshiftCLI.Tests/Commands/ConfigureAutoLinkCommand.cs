using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class ConfigureAutoLinkCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new ConfigureAutoLinkCommand();
            Assert.NotNull(command);
            Assert.Equal("configure-autolink", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
        }
    }
}
