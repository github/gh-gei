using System;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class ShareServiceConnectionCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ShareServiceConnectionCommand();
            Assert.NotNull(command);
            Assert.Equal("share-service-connection", command.Name);
            Assert.Equal(3, command.Options.Count);

            Assert.Equal("ado-org", command.Options[0].Name);
            Assert.True(command.Options[0].IsRequired);
            Assert.Equal("ado-team-project", command.Options[1].Name);
            Assert.True(command.Options[1].IsRequired);
            Assert.Equal("service-connection-id", command.Options[2].Name);
            Assert.True(command.Options[2].IsRequired);
        }
    }
}
