using System;
using System.Linq;
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
    public class AdoPipelineTriggerService_BranchPolicyTests
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

        public AdoPipelineTriggerService_BranchPolicyTests()
        {
            _triggerService = new AdoPipelineTriggerService(_mockAdoApi.Object, _mockOctoLogger.Object, ADO_SERVICE_URL);
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_True_When_Pipeline_Is_Required()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "false"
            }.ToJson();

            var policyResponse = new
            {
                count = 1,
                value = new[]
                {
                    new
                    {
                        id = 1,
                        type = new
                        {
                            id = "0609b952-1397-4640-95ec-e00a01b2c241",
                            displayName = "Build"
                        },
                        isEnabled = true,
                        settings = new
                        {
                            buildDefinitionId = PIPELINE_ID,
                            displayName = PIPELINE_NAME,
                            validDuration = 0
                        }
                    }
                }
            }.ToJson();

            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Use_Repository_Id_Directly_When_Provided()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();

            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "false"
            }.ToJson();

            var policyResponse = new
            {
                count = 1,
                value = new[]
                {
                    new
                    {
                        id = 1,
                        type = new
                        {
                            id = "0609b952-1397-4640-95ec-e00a01b2c241",
                            displayName = "Build"
                        },
                        isEnabled = true,
                        settings = new
                        {
                            buildDefinitionId = PIPELINE_ID,
                            displayName = PIPELINE_NAME,
                            validDuration = 0
                        }
                    }
                }
            }.ToJson();

            // When repository ID is provided, we still need to fetch repo details to check if it's disabled
            var repoByIdUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{repositoryId.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoByIdUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act - Pass repository ID directly instead of name
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, repositoryId, PIPELINE_ID);

            // Assert
            result.Should().BeTrue();

            // Verify that repository lookup by name was NOT called since we provided the ID
            var repoByNameUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            _mockAdoApi.Verify(m => m.GetAsync(repoByNameUrl), Times.Never);
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_Pipeline_Not_In_Policy()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "false"
            }.ToJson();

            var policyResponse = new
            {
                count = 1,
                value = new[]
                {
                    new
                    {
                        id = 1,
                        type = new
                        {
                            id = "0609b952-1397-4640-95ec-e00a01b2c241",
                            displayName = "Build"
                        },
                        isEnabled = true,
                        settings = new
                        {
                            buildDefinitionId = 999, // Different pipeline ID
                            displayName = "Other Pipeline",
                            validDuration = 0
                        }
                    }
                }
            }.ToJson();

            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_Policy_Disabled()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "false"
            }.ToJson();

            var policyResponse = new
            {
                count = 1,
                value = new[]
                {
                    new
                    {
                        id = 1,
                        type = new
                        {
                            id = "0609b952-1397-4640-95ec-e00a01b2c241",
                            displayName = "Build"
                        },
                        isEnabled = false, // Policy is disabled
                        settings = new
                        {
                            buildDefinitionId = PIPELINE_ID,
                            displayName = PIPELINE_NAME,
                            validDuration = 0
                        }
                    }
                }
            }.ToJson();

            var repoUrl = $"/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_No_Build_Policies()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "false"
            }.ToJson();

            var policyResponse = new
            {
                count = 0,
                value = Array.Empty<object>()
            }.ToJson();

            var repoUrl = $"/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_Repository_Is_Disabled()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "true"
            }.ToJson();

            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();

            // Verify that branch policy check was NOT performed since repository is disabled
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";
            _mockAdoApi.Verify(m => m.GetAsync(policyUrl), Times.Never);
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_Repository_Returns_404()
        {
            // Arrange - Disabled repositories often return 404 when queried directly
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(repoUrl))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

            // Act
            var result = await _triggerService.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, null, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();

            // Verify that branch policy check was NOT performed since repository returned 404
            _mockAdoApi.Verify(m => m.GetAsync(It.Is<string>(url => url.Contains("policy/configurations"))), Times.Never);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_WhenPipelineNotRequiredByBranchPolicy_ShouldPreserveExistingTriggers()
        {
            // Arrange - Scenario 1: Pipeline NOT required by branch policy, preserve existing triggers
            var githubRepo = "test-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var pipelineId = PIPELINE_ID;
            var clean = "true";
            var checkoutSubmodules = "false";

            // Mock existing pipeline with PR and CI triggers enabled
            var originalTriggers = JArray.Parse(@"[
                {
                    'triggerType': 'continuousIntegration',
                    'branchFilters': ['+refs/heads/main']
                },
                {
                    'triggerType': 'pullRequest',
                    'branchFilters': ['+refs/heads/main']
                }
            ]");

            var existingPipelineData = new
            {
                name = PIPELINE_NAME,
                repository = new { name = REPO_NAME },
                triggers = originalTriggers,
                someOtherProperty = "value"
            };

            // Mock repository lookup - return valid repository
            var repositoryId = "repo-123";
            var repoResponse = new { id = repositoryId, name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            // Mock branch policies - return empty policies (not required by branch policy)
            var policies = new
            {
                count = 0,
                value = Array.Empty<object>()
            }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policies);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, originalTriggers, null);

            // Assert - Should preserve original triggers (both CI and PR, with build status reporting)
            result.Should().BeTrue();
            _mockAdoApi.Verify(m => m.PutAsync(pipelineUrl, It.Is<object>(payload =>
                VerifyTriggersPreserved(payload, true, true)
            )), Times.Once);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_WhenPipelineRequiredByBranchPolicy_ShouldEnableTriggersWithBuildStatus()
        {
            // Arrange - Scenario 2: Pipeline IS required by branch policy, enable CI + PR + build status
            var githubRepo = "test-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var pipelineId = PIPELINE_ID;
            var clean = "true";
            var checkoutSubmodules = "false";

            // Mock existing pipeline (original triggers don't matter when required by branch policy)
            var existingPipelineData = new
            {
                name = PIPELINE_NAME,
                repository = new { name = REPO_NAME },
                someOtherProperty = "value"
            };

            // Mock repository lookup - return valid repository
            var repositoryId = "repo-123";
            var repoResponse = new { id = repositoryId, name = REPO_NAME, isDisabled = "false" }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            // Mock branch policies - return policy that requires this pipeline
            var policies = new
            {
                count = 1,
                value = new[]
                {
                    new
                    {
                        id = 1,
                        type = new { id = "123", displayName = "Build" },
                        isEnabled = true,
                        settings = new { buildDefinitionId = PIPELINE_ID, displayName = PIPELINE_NAME, validDuration = 720 }
                    }
                }
            }.ToJson();
            var policyUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoApi.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policies);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, null, null);

            // Assert - Should enable both CI and PR triggers WITH build status reporting
            result.Should().BeTrue();
            _mockAdoApi.Verify(m => m.PutAsync(pipelineUrl, It.Is<object>(payload =>
                VerifyTriggersPreserved(payload, true, true)
            )), Times.Once);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Skip_When_Repository_Is_Disabled()
        {
            // Arrange
            var githubRepo = "test-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var pipelineId = PIPELINE_ID;
            var clean = "true";
            var checkoutSubmodules = "false";
            var repositoryId = Guid.NewGuid().ToString();

            // Mock existing pipeline with disabled repository
            var existingPipelineData = new
            {
                name = PIPELINE_NAME,
                repository = new { name = REPO_NAME, id = repositoryId },
                someOtherProperty = "value"
            };

            // Mock repository lookup - return disabled repository
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME,
                isDisabled = "true"
            }.ToJson();
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{repositoryId.EscapeDataString()}?api-version=6.0";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoApi.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, null, null);

            // Assert - Should succeed and call PutAsync even though repository is disabled
            result.Should().BeTrue();
            _mockAdoApi.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);

            // Verify info message was logged about skipping branch policy check
            _mockOctoLogger.Verify(m => m.LogInformation(It.Is<string>(s =>
                s.Contains("disabled") &&
                s.Contains("Branch policy check skipped") &&
                s.Contains(pipelineId.ToString())
            )), Times.Once);
        }

        [Fact]
        public async Task RewirePipelineToGitHub_Should_Skip_When_Repository_Returns_404()
        {
            // Arrange
            var githubRepo = "test-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "main";
            var pipelineId = PIPELINE_ID;
            var clean = "true";
            var checkoutSubmodules = "false";
            var repositoryId = Guid.NewGuid().ToString();

            // Mock existing pipeline
            var existingPipelineData = new
            {
                name = PIPELINE_NAME,
                repository = new { name = REPO_NAME, id = repositoryId },
                someOtherProperty = "value"
            };

            // Mock repository lookup - return 404 (likely disabled)
            var repoUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{repositoryId.EscapeDataString()}?api-version=6.0";

            var pipelineUrl = $"{ADO_SERVICE_URL}/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoApi.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoApi.Setup(m => m.GetAsync(repoUrl))
                .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

            // Act
            var result = await _triggerService.RewirePipelineToGitHub(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, null, null);

            // Assert - Should succeed and call PutAsync even though repository returned 404
            result.Should().BeTrue();
            _mockAdoApi.Verify(m => m.PutAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);

            // Verify info message was logged about skipping branch policy check (repo treated as disabled)
            _mockOctoLogger.Verify(m => m.LogInformation(It.Is<string>(s =>
                s.Contains("disabled") &&
                s.Contains("Branch policy check skipped") &&
                s.Contains(pipelineId.ToString())
            )), Times.Once);
        }

        private static bool VerifyTriggersPreserved(object payload, bool enablePullRequestValidation, bool enableBuildStatusReporting)
        {
            var json = payload.ToJson();
            var parsedPayload = JObject.Parse(json);

            // Check if triggers exist and have expected configuration
            if (parsedPayload["triggers"] is not JArray triggers)
            {
                return false;
            }

            var prTrigger = triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest") as JObject;

            // Verify CI trigger exists
            if (triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "continuousIntegration") is not JObject ciTrigger)
            {
                return false;
            }

            // Verify PR trigger exists if expected
            if (enablePullRequestValidation && prTrigger == null)
            {
                return false;
            }

            if (!enablePullRequestValidation && prTrigger != null)
            {
                return false;
            }

            // Verify build status reporting if expected
            if (enableBuildStatusReporting)
            {
                var ciReportStatus = ciTrigger["reportBuildStatus"]?.ToString();
                if (ciReportStatus != "true")
                {
                    return false;
                }

                if (prTrigger != null)
                {
                    var prReportStatus = prTrigger["reportBuildStatus"]?.ToString();
                    if (prReportStatus != "true")
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
