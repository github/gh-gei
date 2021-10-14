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

            Assert.Equal("github-org", command.Options[0].Name);
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal("github-repo", command.Options[1].Name);
            Assert.True(command.Options[1].IsRequired);
            Assert.Equal("ado-org", command.Options[2].Name);
            Assert.True(command.Options[2].IsRequired);
            Assert.Equal("ado-team-project", command.Options[3].Name);
            Assert.True(command.Options[3].IsRequired);
        }
    }
}
