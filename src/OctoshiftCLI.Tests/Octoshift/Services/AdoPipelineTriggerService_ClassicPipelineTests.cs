using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class AdoPipelineTriggerService_ClassicPipelineTests
    {
        private const string ADO_ORG = "foo-org";
        private const string TEAM_PROJECT = "foo-project";
        private const string REPO_NAME = "foo-repo";
        private const int PIPELINE_ID = 123;
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly AdoPipelineTriggerService _triggerService;

        public AdoPipelineTriggerService_ClassicPipelineTests()
        {
            _triggerService = new AdoPipelineTriggerService(_mockAdoApi.Object, _mockOctoLogger.Object, ADO_SERVICE_URL);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Use_SettingsSourceType_1_For_Classic_Pipeline()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            var originalTriggers = new JArray
            {
                new JObject
                {
                    ["triggerType"] = "continuousIntegration",
                    ["branchFilters"] = new JArray { "+refs/heads/main" }
                }
            };

            // Classic pipeline definition with process.type = 1
            var existingPipelineData = new JObject
            {
                ["name"] = "classic-build-pipeline",
                ["process"] = new JObject { ["type"] = 1 },
                ["repository"] = new JObject { ["name"] = REPO_NAME, ["id"] = "repo-id-123" },
                ["triggers"] = originalTriggers
            };

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ReturnsAsync(existingPipelineData.ToString());

            // Mock repository lookup for branch policy check
            var repoResponse = new { id = "repo-id-123", name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(repoUrl))
                .ReturnsAsync(repoResponse);

            // Mock branch policies (empty)
            var policies = new { count = 0, value = Array.Empty<object>() }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId=repo-id-123&api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(policyUrl))
                .ReturnsAsync(policies);

            JObject capturedPayload = null;
            _mockAdoApi.Setup(x => x.PutAsync(pipelineUrl, It.IsAny<object>()))
                .Callback<string, object>((_, payload) => capturedPayload = JObject.FromObject(payload))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, originalTriggers, null);

            // Assert
            result.Should().BeTrue();
            capturedPayload.Should().NotBeNull();
            ((int)capturedPayload["settingsSourceType"]).Should().Be(1, "Classic pipelines must use settingsSourceType=1 (UI/Designer)");
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Use_SettingsSourceType_2_For_Yaml_Pipeline()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            // YAML pipeline definition with process.type = 2
            var existingPipelineData = new JObject
            {
                ["name"] = "yaml-pipeline",
                ["process"] = new JObject { ["type"] = 2 },
                ["repository"] = new JObject { ["name"] = REPO_NAME, ["id"] = "repo-id-456" },
                ["triggers"] = new JArray()
            };

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ReturnsAsync(existingPipelineData.ToString());

            // Mock repository lookup
            var repoResponse = new { id = "repo-id-456", name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(repoUrl))
                .ReturnsAsync(repoResponse);

            // Mock branch policies (empty)
            var policies = new { count = 0, value = Array.Empty<object>() }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId=repo-id-456&api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(policyUrl))
                .ReturnsAsync(policies);

            JObject capturedPayload = null;
            _mockAdoApi.Setup(x => x.PutAsync(pipelineUrl, It.IsAny<object>()))
                .Callback<string, object>((_, payload) => capturedPayload = JObject.FromObject(payload))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, null, null);

            // Assert
            result.Should().BeTrue();
            capturedPayload.Should().NotBeNull();
            ((int)capturedPayload["settingsSourceType"]).Should().Be(2, "YAML pipelines must use settingsSourceType=2 (YAML definitions)");
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Preserve_Original_Triggers_For_Classic_Pipeline()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            var originalTriggers = new JArray
            {
                new JObject
                {
                    ["triggerType"] = "continuousIntegration",
                    ["branchFilters"] = new JArray { "+refs/heads/main", "+refs/heads/develop" },
                    ["batchChanges"] = true
                }
            };

            // Classic pipeline
            var existingPipelineData = new JObject
            {
                ["name"] = "classic-build-pipeline",
                ["process"] = new JObject { ["type"] = 1 },
                ["repository"] = new JObject { ["name"] = REPO_NAME, ["id"] = "repo-id-123" },
                ["triggers"] = new JArray
                {
                    new JObject { ["triggerType"] = "continuousIntegration", ["branchFilters"] = new JArray { "+refs/heads/old" } }
                }
            };

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ReturnsAsync(existingPipelineData.ToString());

            var repoResponse = new { id = "repo-id-123", name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(repoUrl)).ReturnsAsync(repoResponse);

            var policies = new { count = 0, value = Array.Empty<object>() }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId=repo-id-123&api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(policyUrl)).ReturnsAsync(policies);

            JObject capturedPayload = null;
            _mockAdoApi.Setup(x => x.PutAsync(pipelineUrl, It.IsAny<object>()))
                .Callback<string, object>((_, payload) => capturedPayload = JObject.FromObject(payload))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, originalTriggers, null);

            // Assert
            result.Should().BeTrue();
            capturedPayload.Should().NotBeNull();

            // Classic pipelines should preserve the original triggers, not reconfigure them for YAML
            var triggers = (JArray)capturedPayload["triggers"];
            triggers.Should().NotBeNull();
            triggers.Count.Should().Be(1);
            triggers[0]["triggerType"].ToString().Should().Be("continuousIntegration");
            // Should use the passed-in originalTriggers, which has main+develop
            ((JArray)triggers[0]["branchFilters"]).Count.Should().Be(2);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Default_To_Yaml_When_Process_Type_Missing()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            // Pipeline definition without process.type (legacy or unexpected response)
            var existingPipelineData = new JObject
            {
                ["name"] = "some-pipeline",
                ["repository"] = new JObject { ["name"] = REPO_NAME, ["id"] = "repo-id-789" },
                ["triggers"] = new JArray()
            };

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ReturnsAsync(existingPipelineData.ToString());

            var repoResponse = new { id = "repo-id-789", name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(repoUrl)).ReturnsAsync(repoResponse);

            var policies = new { count = 0, value = Array.Empty<object>() }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId=repo-id-789&api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(policyUrl)).ReturnsAsync(policies);

            JObject capturedPayload = null;
            _mockAdoApi.Setup(x => x.PutAsync(pipelineUrl, It.IsAny<object>()))
                .Callback<string, object>((_, payload) => capturedPayload = JObject.FromObject(payload))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, null, null);

            // Assert - should default to YAML behavior (settingsSourceType=2)
            result.Should().BeTrue();
            capturedPayload.Should().NotBeNull();
            ((int)capturedPayload["settingsSourceType"]).Should().Be(2, "When process type is missing, should default to YAML (settingsSourceType=2)");
        }
    }
}
