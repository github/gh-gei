using System.Linq;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class AdoPipelineTriggerService_PullRequestValidationTests
    {
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly AdoPipelineTriggerService _service;

        public AdoPipelineTriggerService_PullRequestValidationTests()
        {
            _service = new AdoPipelineTriggerService(_mockAdoApi.Object, _mockOctoLogger.Object, ADO_SERVICE_URL);
        }

        [Fact]
        public void CreateYamlControlledTriggers_Should_Create_PullRequest_Trigger_When_EnablePullRequestValidation_Is_True()
        {
            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("CreateYamlControlledTriggers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JArray)method.Invoke(_service, new object[] { true, false, false });

            // Assert
            result.Should().NotBeNull();
            var triggers = result;
            triggers.Should().HaveCount(2); // CI trigger + PR trigger

            // Find the PR trigger using pattern matching
            if (triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest") is not JObject prTrigger)
            {
                Assert.Fail("PR trigger should exist when enablePullRequestValidation is true");
                return; // This won't be reached, but helps with null analysis
            }

            prTrigger["triggerType"].ToString().Should().Be("pullRequest");
            prTrigger["isCommentRequiredForPullRequest"].Value<bool>().Should().BeFalse();
            prTrigger["requireCommentsForNonTeamMembersOnly"].Value<bool>().Should().BeFalse();

            var forks = (JObject)prTrigger["forks"];
            forks["enabled"].Value<bool>().Should().BeFalse();
            forks["allowSecrets"].Value<bool>().Should().BeFalse();

            var branchFilters = (JArray)prTrigger["branchFilters"];
            branchFilters.Should().BeEmpty(); // Empty means defer to YAML

            var pathFilters = (JArray)prTrigger["pathFilters"];
            pathFilters.Should().BeEmpty(); // No path restrictions
        }

        [Fact]
        public void CreateYamlControlledTriggers_Should_Not_Create_PullRequest_Trigger_When_EnablePullRequestValidation_Is_False()
        {
            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("CreateYamlControlledTriggers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JArray)method.Invoke(_service, new object[] { false, false, false });

            // Assert
            result.Should().NotBeNull();
            var triggers = result;

            // Should not have any pull request triggers
            var prTriggers = triggers
                .OfType<JObject>()
                .Where(t => t["triggerType"]?.ToString() == "pullRequest");

            prTriggers.Should().BeEmpty();
        }

        [Fact]
        public void CreateYamlControlledTriggers_Should_Create_Both_CI_And_PR_Triggers_When_Both_Enabled()
        {
            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("CreateYamlControlledTriggers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act - Enable both CI build status and PR validation
            var result = (JArray)method.Invoke(_service, new object[] { true, true, false });

            // Assert
            result.Should().NotBeNull();
            var triggers = result;
            triggers.Should().HaveCount(2);

            // Should have CI trigger
            if (triggers.OfType<JObject>().FirstOrDefault(t => t["triggerType"]?.ToString() == "continuousIntegration") is not JObject ciTrigger)
            {
                Assert.Fail("CI trigger should exist");
                return; // This won't be reached, but helps with null analysis
            }

            // Should have PR trigger
            if (triggers.OfType<JObject>().FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest") is not JObject prTrigger)
            {
                Assert.Fail("PR trigger should exist when enablePullRequestValidation is true");
                return; // This won't be reached, but helps with null analysis
            }

            // Verify PR trigger configuration
            prTrigger["isCommentRequiredForPullRequest"].Value<bool>().Should().BeFalse();
            prTrigger["requireCommentsForNonTeamMembersOnly"].Value<bool>().Should().BeFalse();

            var forks = (JObject)prTrigger["forks"];
            forks["enabled"].Value<bool>().Should().BeFalse();
            forks["allowSecrets"].Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void HasPullRequestTrigger_Should_Return_True_When_Triggers_Contain_PullRequest_Type()
        {
            // Arrange
            var originalTriggers = JArray.Parse(@"[
                {
                    'triggerType': 'continuousIntegration',
                    'branchFilters': ['+refs/heads/main']
                },
                {
                    'triggerType': 'pullRequest',
                    'branchFilters': ['+refs/heads/develop']
                }
            ]");

            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("HasPullRequestTrigger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (bool)method.Invoke(_service, new object[] { originalTriggers });

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasPullRequestTrigger_Should_Return_False_When_No_PullRequest_Triggers_Exist()
        {
            // Arrange
            var originalTriggers = JArray.Parse(@"[
                {
                    'triggerType': 'continuousIntegration',
                    'branchFilters': ['+refs/heads/main']
                },
                {
                    'triggerType': 'scheduleOnlyWithChanges',
                    'schedules': [{
                        'branchFilters': ['+refs/heads/main'],
                        'timeZoneId': 'UTC',
                        'startHours': 2,
                        'startMinutes': 0,
                        'daysToBuild': 'monday'
                    }]
                }
            ]");

            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("HasPullRequestTrigger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (bool)method.Invoke(_service, new object[] { originalTriggers });

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasPullRequestTrigger_Should_Return_False_When_Triggers_Are_Null()
        {
            // Use reflection to access the private method for testing
            var method = typeof(AdoPipelineTriggerService).GetMethod("HasPullRequestTrigger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (bool)method.Invoke(_service, new object[] { null });

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void RewirePipelineToGitHub_Should_Create_PullRequest_Trigger_In_Complex_Scenario()
        {
            // Arrange - Mock API responses for a complex scenario where pipeline is required by branch policy
            var adoOrg = "TestOrg";
            var teamProject = "TestProject";
            var pipelineId = 123;
            var defaultBranch = "main";
            var githubOrg = "github-org";
            var githubRepo = "test-repo";

            // Mock branch policies response
            _mockAdoApi.Setup(x => x.GetAsync(It.Is<string>(url => url.Contains("_apis/policy/configurations"))))
                .ReturnsAsync(@"{
                    'value': [
                        {
                            'isEnabled': true,
                            'isBlocking': true,
                            'type': {
                                'id': 'fa4e907d-c16b-4a4c-9dfa-4906e5d171dd'
                            },
                            'settings': {
                                'buildDefinitionId': 123,
                                'queueOnSourceUpdateOnly': false,
                                'manualQueueOnly': false,
                                'displayName': 'Test Build Policy',
                                'validDuration': 720
                            }
                        }
                    ]
                }");

            // Mock pipeline definition response
            _mockAdoApi.Setup(x => x.GetAsync(It.Is<string>(url => url.Contains($"_apis/build/definitions/{pipelineId}"))))
                .ReturnsAsync(@"{
                    'triggers': [
                        {
                            'triggerType': 'continuousIntegration',
                            'branchFilters': ['+refs/heads/main']
                        }
                    ]
                }");

            // Mock PUT request for updating pipeline
            _mockAdoApi.Setup(x => x.PutAsync(It.Is<string>(url => url.Contains($"_apis/build/definitions/{pipelineId}")), It.IsAny<object>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            // Act
            var task = _service.RewirePipelineToGitHub(adoOrg, teamProject, pipelineId, defaultBranch, "true", "true", githubOrg, githubRepo, null);

            // Assert - The method should complete without throwing
            task.Should().NotBeNull();

            // Verify that the PUT was called (indicating pipeline update happened)
            _mockAdoApi.Verify(x => x.PutAsync(It.Is<string>(url => url.Contains($"_apis/build/definitions/{pipelineId}")),
                It.Is<object>(payload => VerifyPullRequestTriggerIncluded(payload))), Times.Once);
        }

        private static bool VerifyPullRequestTriggerIncluded(object payload)
        {
            var json = JObject.FromObject(payload);

            if (json["triggers"] is not JArray triggers)
            {
                return false;
            }

            // Should have a pull request trigger since pipeline is required by branch policy
            if (triggers.FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest") is not JObject prTrigger)
            {
                return false;
            }

            // Verify PR trigger configuration
            var isCommentRequired = prTrigger["isCommentRequiredForPullRequest"]?.Value<bool>();
            var requireCommentsForNonTeamMembers = prTrigger["requireCommentsForNonTeamMembersOnly"]?.Value<bool>();

            if (isCommentRequired != false || requireCommentsForNonTeamMembers != false)
            {
                return false;
            }

            if (prTrigger["forks"] is not JObject forks)
            {
                return false;
            }

            var forksEnabled = forks["enabled"]?.Value<bool>();
            var allowSecrets = forks["allowSecrets"]?.Value<bool>();

            return forksEnabled == false && allowSecrets == false;
        }
    }
}
