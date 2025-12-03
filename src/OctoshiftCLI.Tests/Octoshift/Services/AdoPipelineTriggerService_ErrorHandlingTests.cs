using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class AdoPipelineTriggerService_ErrorHandlingTests
    {
        private const string ADO_ORG = "foo-org";
        private const string TEAM_PROJECT = "foo-project";
        private const string REPO_NAME = "foo-repo";
        private const string PIPELINE_NAME = "CI Pipeline";
        private const int PIPELINE_ID = 123;
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly AdoPipelineTriggerService _triggerService;

        public AdoPipelineTriggerService_ErrorHandlingTests()
        {
            _triggerService = new AdoPipelineTriggerService(_mockAdoApi.Object, _mockOctoLogger.Object, ADO_SERVICE_URL);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Skip_When_Pipeline_Not_Found_404()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            // Mock 404 error when trying to get pipeline definition
            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, null, null);

            // Assert - Should return false indicating skipped
            result.Should().BeFalse();

            // Verify that warning was logged
            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(s =>
                s.Contains("Pipeline 123 not found") &&
                s.Contains("Skipping pipeline rewiring"))), Times.Once);

            // Verify that PutAsync was never called since we should skip the operation
            _mockAdoApi.Verify(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Skip_When_Pipeline_HTTP_Error()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";

            // Mock HTTP error (not 404) when trying to get pipeline definition
            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 500 (Internal Server Error)."));

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, null, null);

            // Assert - Should return false indicating skipped
            result.Should().BeFalse();

            // Verify that warning was logged
            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(s =>
                s.Contains("HTTP error retrieving pipeline 123") &&
                s.Contains("Skipping pipeline rewiring"))), Times.Once);

            // Verify that PutAsync was never called since we should skip the operation
            _mockAdoApi.Verify(x => x.PutAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Continue_When_Pipeline_Found()
        {
            // Arrange
            var githubOrg = "github-org";
            var githubRepo = "github-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var clean = "true";
            var checkoutSubmodules = "false";

            var existingPipelineData = new
            {
                name = PIPELINE_NAME,
                repository = new { name = REPO_NAME },
                triggers = new JArray()
            };

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{PIPELINE_ID}?api-version=6.0";
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            // Mock successful pipeline retrieval
            _mockAdoApi.Setup(x => x.GetAsync(pipelineUrl))
                .ReturnsAsync(existingPipelineData.ToJson());

            // Mock repository lookup for branch policy check
            var repositoryId = "repo-123";
            var repoResponse = new { id = repositoryId, name = REPO_NAME, isDisabled = "false" }.ToJson();
            _mockAdoApi.Setup(x => x.GetAsync(repoUrl))
                .ReturnsAsync(repoResponse);

            // Mock branch policies (empty)
            var policies = new { count = 0, value = Array.Empty<object>() }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";
            _mockAdoApi.Setup(x => x.GetAsync(policyUrl))
                .ReturnsAsync(policies);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(
                ADO_ORG, TEAM_PROJECT, PIPELINE_ID, defaultBranch, clean, checkoutSubmodules,
                githubOrg, githubRepo, serviceConnectionId, null, null);

            // Assert - Should return true indicating successful rewiring
            result.Should().BeTrue();

            // Verify that PutAsync was called (pipeline was successfully rewired)
            _mockAdoApi.Verify(x => x.PutAsync(pipelineUrl, It.IsAny<object>()), Times.Once);

            // Verify that no error warnings were logged
            _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(s =>
                s.Contains("not found") || s.Contains("HTTP error"))), Times.Never);
        }
    }
}
