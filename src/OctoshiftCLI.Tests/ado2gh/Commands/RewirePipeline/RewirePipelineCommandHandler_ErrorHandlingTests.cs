using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.RewirePipeline
{
    public class RewirePipelineCommandHandler_ErrorHandlingTests
    {
        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_PIPELINE = "FooPipeline";
        private const int PIPELINE_ID = 123;
        private const string GITHUB_ORG = "GitHubOrg";
        private const string GITHUB_REPO = "GitHubRepo";
        private const string SERVICE_CONNECTION_ID = "1234";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoPipelineTriggerService> _mockAdoPipelineTriggerService;
        private readonly RewirePipelineCommandHandler _handler;

        public RewirePipelineCommandHandler_ErrorHandlingTests()
        {
            _mockAdoPipelineTriggerService = new Mock<AdoPipelineTriggerService>(_mockAdoApi.Object, _mockOctoLogger.Object, "https://dev.azure.com");
            _handler = new RewirePipelineCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockAdoPipelineTriggerService.Object);
        }

        [Fact]
        public async Task HandleRegularRewire_Should_Throw_OctoshiftCliException_When_Pipeline_Not_Found()
        {
            // Arrange
            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE))
                .ReturnsAsync(PIPELINE_ID);

            _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, PIPELINE_ID))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
                DryRun = false
            };

            // Act & Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should().ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("Pipeline could not be found. Please verify the pipeline name or ID and try again.");

            // Verify error was logged
            _mockOctoLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Pipeline not found"))), Times.Once);
        }

        [Fact]
        public async Task HandleRegularRewire_Should_Throw_OctoshiftCliException_When_Pipeline_Lookup_Fails()
        {
            // Arrange
            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE))
                .ThrowsAsync(new ArgumentException("Unable to find the specified pipeline", "pipeline"));

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
                DryRun = false
            };

            // Act & Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should().ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("Unable to find the specified pipeline. Please verify the pipeline name and try again.");

            // Verify error was logged
            _mockOctoLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Pipeline lookup failed"))), Times.Once);
        }

        [Fact]
        public async Task HandleRegularRewire_Should_Succeed_When_Pipeline_Found()
        {
            // Arrange
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";
            var triggers = new JArray();

            _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE))
                .ReturnsAsync(PIPELINE_ID);

            _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, PIPELINE_ID))
                .ReturnsAsync((defaultBranch, clean, checkoutSubmodules, triggers));

            _mockAdoPipelineTriggerService.Setup(x => x.RewirePipelineToGitHub(
                ADO_ORG, ADO_TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID, triggers, null))
                .ReturnsAsync(true);

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoPipeline = ADO_PIPELINE,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ServiceConnectionId = SERVICE_CONNECTION_ID,
                DryRun = false
            };

            // Act
            await _handler.Handle(args);

            // Assert
            _mockAdoPipelineTriggerService.Verify(x => x.RewirePipelineToGitHub(
                ADO_ORG, ADO_TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID, triggers, null), Times.Once);

            // Verify success was logged
            _mockOctoLogger.Verify(x => x.LogSuccess("Successfully rewired pipeline"), Times.Once);

            // Verify no errors were logged
            _mockOctoLogger.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
        }
    }
}
