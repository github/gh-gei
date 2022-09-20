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

        private readonly RewirePipelineCommandHandler _command;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_PIPELINE = "foo-pipeline";
        private const string GITHUB_ORG = "foo-gh-org";
        private const string GITHUB_REPO = "gh-repo";
        private readonly string SERVICE_CONNECTION_ID = Guid.NewGuid().ToString();
        private readonly string ADO_PAT = Guid.NewGuid().ToString();

        public RewirePipelineCommandTests()
        {
            _command = new RewirePipelineCommandHandler(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new RewirePipelineCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(8, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var pipelineId = 1234;
            var defaultBranch = "default-branch";
            var clean = "true";
            var checkoutSubmodules = "null";

            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules));

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
            };
            await _command.Invoke(args);

            _mockAdoApi.Verify(x => x.ChangePipelineRepo(ADO_ORG, ADO_TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            _mockAdoApiFactory.Setup(m => m.Create(ADO_PAT)).Returns(_mockAdoApi.Object);

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = Guid.NewGuid().ToString(),
                AdoPat = ADO_PAT,
            };
            await _command.Invoke(args);

            _mockAdoApiFactory.Verify(m => m.Create(ADO_PAT));
        }
    }
}
