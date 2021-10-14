using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class MigrateRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("migrate-repo", command.Name);
            Assert.Equal(5, command.Options.Count);

            Helpers.VerifyCommandOption(command.Options, "ado-org", true);
            Helpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            Helpers.VerifyCommandOption(command.Options, "ado-repo", true);
            Helpers.VerifyCommandOption(command.Options, "github-org", true);
            Helpers.VerifyCommandOption(command.Options, "github-repo", true);
        }
    }
}
