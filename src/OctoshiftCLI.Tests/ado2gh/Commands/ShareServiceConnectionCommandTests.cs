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
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ShareServiceConnectionCommand _command;

        public ShareServiceConnectionCommandTests()
        {
            _command = new ShareServiceConnectionCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("share-service-connection", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();

            _mockAdoApi.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            _mockAdoApi.Setup(x => x.ContainsServiceConnection(adoOrg, adoTeamProject, serviceConnectionId).Result).Returns(false);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            _mockAdoApi.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId));
        }

        [Fact]
        public async Task It_Skips_When_Already_Shared()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();

            _mockAdoApi.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            _mockAdoApi.Setup(x => x.ContainsServiceConnection(adoOrg, adoTeamProject, serviceConnectionId).Result).Returns(true);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, serviceConnectionId);

            _mockAdoApi.Verify(x => x.ShareServiceConnection(adoOrg, adoTeamProject, teamProjectId, serviceConnectionId), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "serviceConnectionId", adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
