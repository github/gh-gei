using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class RewirePipelineCommandTests
    {
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly RewirePipelineCommand _command;

        public RewirePipelineCommandTests()
        {
            _command = new RewirePipelineCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("rewire-pipeline", _command.Name);
            Assert.Equal(9, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "default-branch", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoPipeline = "foo-pipeline";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var defaultBranch = "default-branch";
            var clean = "true";
            var checkoutSubmodules = "null";

            _mockAdoApi.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules));

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId);

            _mockAdoApi.Verify(x => x.ChangePipelineRepo(adoOrg, adoTeamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "adoPipeline", "githubOrg", "githubRepo", "serviceConnectionId", adoPat: adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }

        [Fact]
        public async Task Command_Uses_Existing_Default_Branch_When_Option_Not_Present()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoPipeline = "foo-pipeline";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var defaultBranch = "default-branch";
            var clean = "true";
            var checkoutSubmodules = "null";

            const string adoPat = "ado-pat";

            _mockAdoApi.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules));
            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);


            await _command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId, defaultBranch: null, adoPat: adoPat);

            _mockAdoApi.Verify(x => x.ChangePipelineRepo(adoOrg, adoTeamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId));
        }

        /// <summary>
        /// Ensures that when the default branch option is specified the old pipeline default branch value is not used.
        /// </summary>
        [Fact]
        public async Task Command_Uses_Default_Branch_Option_Not_Existing_Default_Branch()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoPipeline = "foo-pipeline";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var defaultBranch = "default-branch";
            var clean = "true";
            var checkoutSubmodules = "null";

            const string adoPat = "ado-pat";

            _mockAdoApi.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns(("old-default-branch", clean, checkoutSubmodules));
            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId, adoPat: adoPat, defaultBranch: defaultBranch);

            _mockAdoApi.Verify(x => x.ChangePipelineRepo(adoOrg, adoTeamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId));
        }
    }
}
