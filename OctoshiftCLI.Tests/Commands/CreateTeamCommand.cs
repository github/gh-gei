using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class CreateTeamCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new CreateTeamCommand();
            Assert.NotNull(command);
            Assert.Equal("create-team", command.Name);
            Assert.Equal(3, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "github-org", true);
            Helpers.VerifyCommandOption(command.Options, "team-name", true);
            Helpers.VerifyCommandOption(command.Options, "idp-group", false);
        }
    }
}
