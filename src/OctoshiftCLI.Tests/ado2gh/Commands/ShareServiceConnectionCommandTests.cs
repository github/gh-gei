using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ShareServiceConnectionCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ShareServiceConnectionCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("share-service-connection", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdo.Object);

            var command = new ShareServiceConnectionCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            mockAdo.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new ShareServiceConnectionCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "serviceConnectionId", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
