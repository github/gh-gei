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

            Assert.Equal("github-org", command.Options[0].Name);
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal("team-name", command.Options[1].Name);
            Assert.True(command.Options[1].IsRequired);
            Assert.Equal("idp-group", command.Options[2].Name);
            Assert.False(command.Options[2].IsRequired);
        }
    }
}
