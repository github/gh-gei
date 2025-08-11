using System;
using System.Linq;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class AdoApi_PullRequestValidationTests
    {
        private const string ADO_SERVICE_URL = "https://dev.azure.com";

        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<AdoClient> _mockAdoClient = TestHelpers.CreateMock<AdoClient>();
        private readonly AdoApi _adoApi;

        public AdoApi_PullRequestValidationTests()
        {
            _adoApi = new AdoApi(_mockAdoClient.Object, ADO_SERVICE_URL, _mockOctoLogger.Object);
        }

        [Fact]
        public void EnsurePullRequestValidationEnabled_Should_Create_PullRequest_Trigger_When_None_Exists()
        {
            // Arrange - no original triggers
            JToken originalTriggers = null;

            // Use reflection to access private method for testing
            var method = typeof(AdoApi).GetMethod("EnsurePullRequestValidationEnabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JToken)method.Invoke(_adoApi, new object[] { originalTriggers });

            // Assert
            result.Should().NotBeNull();
            var triggers = result as JArray;
            triggers.Should().NotBeNull();
            triggers!.Should().HaveCount(1);

            var prTrigger = triggers![0] as JObject;
            prTrigger.Should().NotBeNull();
            prTrigger!["triggerType"].ToString().Should().Be("pullRequest");
            prTrigger["isCommentRequiredForPullRequest"].Value<bool>().Should().BeFalse();
            prTrigger["requireCommentsForNonTeamMembersOnly"].Value<bool>().Should().BeFalse();

            var forks = prTrigger["forks"] as JObject;
            forks.Should().NotBeNull();
            forks!["enabled"].Value<bool>().Should().BeFalse();
            forks["allowSecrets"].Value<bool>().Should().BeFalse();

            var branchFilters = prTrigger["branchFilters"] as JArray;
            branchFilters.Should().NotBeNull();
            branchFilters!.Select(t => t?.Value<string>()).Should().Contain("+refs/heads/*");
        }

        [Fact]
        public void EnsurePullRequestValidationEnabled_Should_Enhance_Existing_PullRequest_Trigger()
        {
            // Arrange - existing triggers with a basic PR trigger
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

            // Use reflection to access private method for testing
            var method = typeof(AdoApi).GetMethod("EnsurePullRequestValidationEnabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JToken)method.Invoke(_adoApi, new object[] { originalTriggers });

            // Assert
            result.Should().NotBeNull();
            var triggers = result as JArray;
            triggers.Should().NotBeNull();
            triggers!.Should().HaveCount(2); // CI + enhanced PR trigger

            var prTrigger = triggers!
                .OfType<JObject>()
                .FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest");

            prTrigger.Should().NotBeNull();
            prTrigger!["isCommentRequiredForPullRequest"].Value<bool>().Should().BeFalse();
            prTrigger["requireCommentsForNonTeamMembersOnly"].Value<bool>().Should().BeFalse();

            var forks = prTrigger["forks"] as JObject;
            forks.Should().NotBeNull();
            forks!["enabled"].Value<bool>().Should().BeFalse();
            forks["allowSecrets"].Value<bool>().Should().BeFalse();

            // Should preserve original branch filters
            var branchFilters = prTrigger["branchFilters"] as JArray;
            branchFilters.Should().NotBeNull();
            branchFilters!.Select(t => t?.Value<string>()).Should().Contain("+refs/heads/develop");
        }

        [Fact]
        public void EnsurePullRequestValidationEnabled_Should_Preserve_CI_Triggers()
        {
            // Arrange - triggers with CI but no PR trigger
            var originalTriggers = JArray.Parse(@"[
                {
                    'triggerType': 'continuousIntegration',
                    'branchFilters': ['+refs/heads/main', '+refs/heads/develop']
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

            // Use reflection to access private method for testing
            var method = typeof(AdoApi).GetMethod("EnsurePullRequestValidationEnabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JToken)method.Invoke(_adoApi, new object[] { originalTriggers });

            // Assert
            result.Should().NotBeNull();
            var triggers = result as JArray;
            triggers.Should().NotBeNull();
            triggers!.Should().HaveCount(3); // CI + Schedule + new PR trigger

            // Should preserve CI trigger
            var ciTrigger = triggers!
                .OfType<JObject>()
                .FirstOrDefault(t => t["triggerType"]?.ToString() == "continuousIntegration");
            ciTrigger.Should().NotBeNull();
            var ciBranchFilters = ciTrigger!["branchFilters"] as JArray;
            ciBranchFilters.Should().NotBeNull();
            ciBranchFilters!.Select(t => t?.Value<string>()).Should().Contain("+refs/heads/main");
            ciBranchFilters.Select(t => t?.Value<string>()).Should().Contain("+refs/heads/develop");

            // Should preserve schedule trigger
            var scheduleTrigger = triggers!
                .OfType<JObject>()
                .FirstOrDefault(t => t["triggerType"]?.ToString() == "scheduleOnlyWithChanges");
            scheduleTrigger.Should().NotBeNull();

            // Should add PR trigger
            var prTrigger = triggers!
                .OfType<JObject>()
                .FirstOrDefault(t => t["triggerType"]?.ToString() == "pullRequest");
            prTrigger.Should().NotBeNull();
            var forks = prTrigger!["forks"] as JObject;
            forks.Should().NotBeNull();
            forks!["enabled"].Value<bool>().Should().BeFalse();
        }

        [Fact]
        public void CreatePullRequestTrigger_Should_Create_Proper_Default_Configuration()
        {
            // Use reflection to access private method for testing
            var method = typeof(AdoApi).GetMethod("CreatePullRequestTrigger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (JObject)method.Invoke(_adoApi, Array.Empty<object>());

            // Assert
            result.Should().NotBeNull();
            result["triggerType"].ToString().Should().Be("pullRequest");
            result["isCommentRequiredForPullRequest"].Value<bool>().Should().BeFalse();
            result["requireCommentsForNonTeamMembersOnly"].Value<bool>().Should().BeFalse();

            var forks = result["forks"] as JObject;
            forks.Should().NotBeNull();
            forks!["enabled"].Value<bool>().Should().BeFalse();
            forks["allowSecrets"].Value<bool>().Should().BeFalse();

            var pathFilters = result["pathFilters"] as JArray;
            pathFilters.Should().NotBeNull();
            pathFilters!.Should().BeEmpty(); // No path restrictions

            var branchFilters = result["branchFilters"] as JArray;
            branchFilters.Should().NotBeNull();
            branchFilters!.Select(t => t?.Value<string>()).Should().Contain("+refs/heads/*");
        }
    }
}
