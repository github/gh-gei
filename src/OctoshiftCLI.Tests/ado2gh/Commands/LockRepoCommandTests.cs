using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class LockRepoCommandTests
    {
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly LockRepoCommand _command;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_REPO = "foo-repo";
        private readonly string REPO_ID = Guid.NewGuid().ToString();
        private const string IDENTITY_DESCRIPTOR = "foo-id";
        private readonly string TEAM_PROJECT_ID = Guid.NewGuid().ToString();

        public LockRepoCommandTests()
        {
            _command = new LockRepoCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("lock-ado-repo", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
            _mockAdoApi.Setup(x => x.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO).Result).Returns(REPO_ID);
            _mockAdoApi.Setup(x => x.GetIdentityDescriptor(ADO_ORG, TEAM_PROJECT_ID, "Project Valid Users").Result).Returns(IDENTITY_DESCRIPTOR);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            var args = new LockRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO
            };
            await _command.Invoke(args);

            _mockAdoApi.Verify(x => x.LockRepo(ADO_ORG, TEAM_PROJECT_ID, REPO_ID, IDENTITY_DESCRIPTOR));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            var args = new LockRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                AdoPat = adoPat
            };
            await _command.Invoke(args);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
