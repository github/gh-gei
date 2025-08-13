using System;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.RewirePipeline;

    public class RewirePipelineCommandHandlerTests
    {
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoPipelineTriggerService> _mockAdoPipelineTriggerService;

        private readonly RewirePipelineCommandHandler _handler;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_PIPELINE = "foo-pipeline";
        private const string GITHUB_ORG = "foo-gh-org";
        private const string GITHUB_REPO = "foo-gh-repo";
        private const string SERVICE_CONNECTION_ID = "service-connection-123";
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        public RewirePipelineCommandHandlerTests()
        {
            _mockAdoPipelineTriggerService = new Mock<AdoPipelineTriggerService>(_mockAdoApi.Object, _mockOctoLogger.Object, "https://dev.azure.com");
            _handler = new RewirePipelineCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockAdoPipelineTriggerService.Object);
        }    [Fact]
    public async Task Happy_Path()
    {
        var pipelineId = 1234;
        var defaultBranch = "default-branch";
        var clean = "true";
        var checkoutSubmodules = "null";
        var triggers = new JArray(); // Mock triggers data
        var pipelineResponse = "{\"repository\": {\"name\": \"test-repo\"}}";
        var repoResponse = "{\"id\": \"repo-123\"}";
        var policyResponse = "{\"value\": []}";

        _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
        _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules, triggers));

        // Mock the GetAsync calls that AdoPipelineTriggerService makes
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/build/definitions/{pipelineId}?api-version=6.0"))
            .ReturnsAsync(pipelineResponse);
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/git/repositories/test-repo?api-version=6.0"))
            .ReturnsAsync(repoResponse);
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/policy/configurations?repositoryId=repo-123&api-version=6.0"))
            .ReturnsAsync(policyResponse);
        _mockAdoApi.Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        var args = new RewirePipelineCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoPipeline = ADO_PIPELINE,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoPipelineTriggerService.Verify(x => x.RewirePipelineToGitHub(ADO_ORG, ADO_TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID, triggers, null));
    }

    [Fact]
    public async Task Uses_TargetApiUrl_When_Provided()
    {
        var pipelineId = 1234;
        var defaultBranch = "default-branch";
        var clean = "true";
        var checkoutSubmodules = "null";
        var targetApiUrl = "https://api.ghec.example.com";
        var triggers = new JArray(); // Mock triggers data
        var pipelineResponse = "{\"repository\": {\"name\": \"test-repo\"}}";
        var repoResponse = "{\"id\": \"repo-123\"}";
        var policyResponse = "{\"value\": []}";

        _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
        _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules, triggers));

        // Mock the GetAsync calls that AdoPipelineTriggerService makes
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/build/definitions/{pipelineId}?api-version=6.0"))
            .ReturnsAsync(pipelineResponse);
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/git/repositories/test-repo?api-version=6.0"))
            .ReturnsAsync(repoResponse);
        _mockAdoApi.Setup(x => x.GetAsync($"{ADO_SERVICE_URL}/{ADO_ORG}/{ADO_TEAM_PROJECT}/_apis/policy/configurations?repositoryId=repo-123&api-version=6.0"))
            .ReturnsAsync(policyResponse);
        _mockAdoApi.Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        var args = new RewirePipelineCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoPipeline = ADO_PIPELINE,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
            TargetApiUrl = targetApiUrl
        };
        await _handler.Handle(args);

        _mockAdoPipelineTriggerService.Verify(x => x.RewirePipelineToGitHub(ADO_ORG, ADO_TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID, triggers, targetApiUrl));
    }
}
