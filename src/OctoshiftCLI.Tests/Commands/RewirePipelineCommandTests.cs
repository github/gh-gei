using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class RewirePipelineCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new RewirePipelineCommand();
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(6, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "ado-org", true);
            Helpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            Helpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            Helpers.VerifyCommandOption(command.Options, "github-org", true);
            Helpers.VerifyCommandOption(command.Options, "github-repo", true);
            Helpers.VerifyCommandOption(command.Options, "service-connection-id", true);
        }
    }
}
