using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class GenerateScriptCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand();
            Assert.NotNull(command);
            Assert.Equal("generate-script", command.Name);
            Assert.Equal(5, command.Options.Count);

            Assert.Equal("github-org", command.Options[0].Name);
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal("ado-org", command.Options[1].Name);
            Assert.False(command.Options[1].IsRequired);
            Assert.Equal("output", command.Options[2].Name);
            Assert.False(command.Options[2].IsRequired);
            Assert.Equal("repos-only", command.Options[3].Name);
            Assert.False(command.Options[3].IsRequired);
            Assert.Equal("skip-idp", command.Options[4].Name);
            Assert.False(command.Options[4].IsRequired);
        }
    }
}
