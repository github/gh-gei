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

            Assert.Equal(command.Options[0].Name, "github-org");
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal(command.Options[1].Name, "github-repo");
            Assert.True(command.Options[1].IsRequired);
            Assert.Equal(command.Options[2].Name, "team");
            Assert.True(command.Options[2].IsRequired);
            Assert.Equal(command.Options[3].Name, "role");
            Assert.True(command.Options[3].IsRequired);
        }
    }
}
