using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.RewirePipeline
{
    public class RewirePipelineCommandHandler_TriggerPreservationTests
    {
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoPipelineTriggerService> _mockAdoPipelineTriggerService;

        private readonly RewirePipelineCommandHandler _handler;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_PIPELINE = "foo-pipeline";
        private const string GITHUB_ORG = "foo-gh-org";
        private const string GITHUB_REPO = "gh-repo";
        private readonly string SERVICE_CONNECTION_ID = System.Guid.NewGuid().ToString();

        public RewirePipelineCommandHandler_TriggerPreservationTests()
        {
            _mockAdoPipelineTriggerService = new Mock<AdoPipelineTriggerService>(_mockAdoApi.Object, _mockOctoLogger.Object, "https://dev.azure.com");
            _handler = new RewirePipelineCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockAdoPipelineTriggerService.Object);
        }

        [Fact]
        public async Task Should_Preserve_PullRequest_Triggers()
        {
            // Arrange
            var pipelineId = 1234;
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            // Mock triggers that include pull request configuration
            var triggers = JArray.Parse(@"[
                {
                    'triggerType': 'continuousIntegration',
                    'branchFilters': ['+refs/heads/main']
                },
                {
                    'triggerType': 'pullRequest',
                    'forks': {
                        'enabled': true,
                        'allowSecrets': false
                    },
                    'branchFilters': ['+refs/heads/*']
                }
            ]");

            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules, triggers));

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
            };

            // Act
            await _handler.Handle(args);

            // Assert
            _mockAdoPipelineTriggerService.Verify(x => x.RewirePipelineToGitHub(
                ADO_ORG,
                ADO_TEAM_PROJECT,
                pipelineId,
                defaultBranch,
                clean,
                checkoutSubmodules,
                GITHUB_ORG,
                GITHUB_REPO,
                SERVICE_CONNECTION_ID,
                triggers, // Verify that the original triggers are passed through
                null), Times.Once);
        }

        [Fact]
        public async Task Should_Handle_Empty_Triggers()
        {
            // Arrange
            var pipelineId = 1234;
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";
            JToken triggers = null; // No triggers defined

            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
            _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules, triggers));

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
            };

            // Act
            await _handler.Handle(args);

            // Assert
            _mockAdoPipelineTriggerService.Verify(x => x.RewirePipelineToGitHub(
                ADO_ORG,
                ADO_TEAM_PROJECT,
                pipelineId,
                defaultBranch,
                clean,
                checkoutSubmodules,
                GITHUB_ORG,
                GITHUB_REPO,
                SERVICE_CONNECTION_ID,
                null, // Should handle null triggers gracefully
                null), Times.Once);
        }
    }
}
