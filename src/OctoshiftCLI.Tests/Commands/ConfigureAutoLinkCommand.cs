using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class ConfigureAutoLinkCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ConfigureAutoLinkCommand();
            Assert.NotNull(command);
            Assert.Equal("configure-auto-link", command.Name);
            Assert.Equal(4, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "github-org", true);
            Helpers.VerifyCommandOption(command.Options, "github-repo", true);
            Helpers.VerifyCommandOption(command.Options, "ado-org", true);
            Helpers.VerifyCommandOption(command.Options, "ado-team-project", true);
        }
    }
}
