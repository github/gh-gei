using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class AddTeamToRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new AddTeamToRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(4, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "github-org", true);
            Helpers.VerifyCommandOption(command.Options, "github-repo", true);
            Helpers.VerifyCommandOption(command.Options, "team", true);
            Helpers.VerifyCommandOption(command.Options, "role", true);
        }
    }
}
