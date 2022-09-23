using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class InventoryReportCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new InventoryReportCommand(null, null, null, null, null, null, null);
            Assert.NotNull(command);
            Assert.Equal("inventory-report", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "minimal", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }
    }
}
