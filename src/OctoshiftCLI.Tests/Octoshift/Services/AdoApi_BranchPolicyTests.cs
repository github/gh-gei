using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class AdoApi_BranchPolicyTests
    {
        private const string ADO_ORG = "foo-org";
        private const string TEAM_PROJECT = "foo-project";
        private const string REPO_NAME = "foo-repo";
        private const string PIPELINE_NAME = "CI Pipeline";
        private const int PIPELINE_ID = 123;
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoClient> _mockAdoClient = TestHelpers.CreateMock<AdoClient>();
        private readonly AdoApi _adoApi;

        public AdoApi_BranchPolicyTests()
        {
            _adoApi = new AdoApi(_mockAdoClient.Object, ADO_SERVICE_URL, _mockOctoLogger.Object);
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_True_When_Pipeline_Is_Required()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME
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

            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _adoApi.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, PIPELINE_ID);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPipelineRequiredByBranchPolicy_Should_Return_False_When_Pipeline_Not_In_Policy()
        {
            // Arrange
            var repositoryId = Guid.NewGuid().ToString();
            var repoResponse = new
            {
                id = repositoryId,
                name = REPO_NAME
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
                            buildDefinitionId = 456, // Different pipeline
                            displayName = "Different Pipeline",
                            validDuration = 0
                        }
                    }
                }
            }.ToJson();

            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _adoApi.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, PIPELINE_ID);

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
                name = REPO_NAME
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

            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _adoApi.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, PIPELINE_ID);

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
                name = REPO_NAME
            }.ToJson();

            var policyResponse = new
            {
                count = 0,
                value = Array.Empty<object>()
            }.ToJson();

            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policyResponse);

            // Act
            var result = await _adoApi.IsPipelineRequiredByBranchPolicy(ADO_ORG, TEAM_PROJECT, REPO_NAME, PIPELINE_ID);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ChangePipelineRepo_WhenPipelineNotRequiredByBranchPolicy_ShouldPreserveExistingTriggers()
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
            var repoResponse = new { id = repositoryId, name = REPO_NAME }.ToJson();
            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

            // Mock branch policies - return empty policies (not required by branch policy)
            var policies = new
            {
                count = 0,
                value = Array.Empty<object>()
            }.ToJson();
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            var pipelineUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policies);

            // Act
            await _adoApi.ChangePipelineRepo(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, originalTriggers);

            // Assert - Should preserve original triggers (both CI and PR, no build status reporting)
            _mockAdoClient.Verify(m => m.PutAsync(pipelineUrl, It.Is<object>(payload =>
                VerifyTriggersPreserved(payload, true, false)
            )), Times.Once);
        }

        [Fact]
        public async Task ChangePipelineRepo_WhenPipelineRequiredByBranchPolicy_ShouldEnableTriggersWithBuildStatus()
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
            var repoResponse = new { id = repositoryId, name = REPO_NAME }.ToJson();
            var repoUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{REPO_NAME.EscapeDataString()}?api-version=6.0";

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
            var policyUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/policy/configurations?repositoryId={repositoryId}&api-version=6.0";

            var pipelineUrl = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            _mockAdoClient.Setup(m => m.GetAsync(pipelineUrl)).ReturnsAsync(existingPipelineData.ToJson());
            _mockAdoClient.Setup(m => m.GetAsync(repoUrl)).ReturnsAsync(repoResponse);
            _mockAdoClient.Setup(m => m.GetAsync(policyUrl)).ReturnsAsync(policies);

            // Act
            await _adoApi.ChangePipelineRepo(ADO_ORG, TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, "github-org", githubRepo, serviceConnectionId, null);

            // Assert - Should enable both CI and PR triggers WITH build status reporting
            _mockAdoClient.Verify(m => m.PutAsync(pipelineUrl, It.Is<object>(payload =>
                VerifyTriggersPreserved(payload, true, true)
            )), Times.Once);
        }

        private static bool VerifyTriggersPreserved(object payload, bool enablePullRequestValidation, bool enableBuildStatusReporting)
        {
            var json = JObject.Parse(payload.ToJson());

            if (json["triggers"] is not JArray triggers)
            {
                return false;
            }

            // Should always have CI trigger
            if (triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "continuousIntegration") is not JObject ciTrigger)
            {
                return false;
            }

            // Check CI trigger build status reporting
            var ciHasBuildStatus = ciTrigger["reportBuildStatus"]?.Value<bool>() == true;
            if (ciHasBuildStatus != enableBuildStatusReporting)
            {
                return false;
            }

            // Check PR trigger presence
            var prTrigger = triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest") as JObject;
            if (enablePullRequestValidation)
            {
                if (prTrigger == null)
                {
                    return false;
                }

                // Check PR trigger build status reporting
                var prHasBuildStatus = prTrigger["reportBuildStatus"]?.Value<bool>() == true;
                if (prHasBuildStatus != enableBuildStatusReporting)
                {
                    return false;
                }
            }
            else
            {
                if (prTrigger != null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
