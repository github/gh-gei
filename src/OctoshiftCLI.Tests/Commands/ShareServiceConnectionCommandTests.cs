using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class ShareServiceConnectionCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new ShareServiceConnectionCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("share-service-connection", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);

            var command = new ShareServiceConnectionCommand(new OctoLogger(), adoFactory);
            await command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            mockAdo.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId));
        }
    }
}