using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class ShareServiceConnectionCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new ShareServiceConnectionCommand();
            Assert.NotNull(command);
            Assert.Equal("share-service-connection", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var adoToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(string.Empty);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            AdoApiFactory.Create = token => token == adoToken ? mockAdo.Object : null;

            var command = new ShareServiceConnectionCommand();
            await command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            mockAdo.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId));
        }

        [Fact]
        public async Task MissingADOPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            AdoApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("ADO_PAT", string.Empty);

            var command = new ShareServiceConnectionCommand();

            using var console = new ConsoleOutput();
            await command.Invoke("foo", "foo", "foo");
            Assert.Contains("ERROR: NO ADO_PAT FOUND", console.GetOuput(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
