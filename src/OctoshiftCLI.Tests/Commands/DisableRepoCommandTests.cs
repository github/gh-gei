using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class DisableRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new DisableRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("disable-ado-repo", command.Name);
            Assert.Equal(3, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "ado-org", true);
            Helpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            Helpers.VerifyCommandOption(command.Options, "ado-org", true);
        }
    }
}
