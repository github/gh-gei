using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject,repo,url,pipeline-count,pr-count,last-push-date,commits-past-year,most-active-contributor";

        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();

        private const string ADO_ORG = "foo-org";
        private readonly IEnumerable<string> ADO_ORGS = new List<string>() { ADO_ORG };
        private const string ADO_TEAM_PROJECT = "foo-tp";
        private readonly IEnumerable<string> ADO_TEAM_PROJECTS = new List<string>() { ADO_TEAM_PROJECT };
        private const string ADO_REPO = "foo-repo";
        private readonly IEnumerable<string> ADO_REPOS = new List<string>() { ADO_REPO };

        private readonly ReposCsvGeneratorService _service;

        public ReposCsvGeneratorServiceTests()
        {
            _mockAdoInspectorServiceFactory.Setup(m => m.Create(_mockAdoApi.Object)).Returns(_mockAdoInspectorService.Object);
            _service = new ReposCsvGeneratorService(_mockAdoInspectorServiceFactory.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Repo()
        {
            // Arrange
            var pipelineCount = 41;
            var prCount = 822;
            var lastPushDate = DateTime.Now;
            var commitCount = 183;
            var pushers = new List<string>() { "Dylan", "Arin", "Arin", "Max" };

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspectorService.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(pipelineCount);
            _mockAdoInspectorService.Setup(m => m.GetPullRequestCount(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(prCount);
            _mockAdoApi.Setup(m => m.GetPushersSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, It.IsAny<DateTime>())).ReturnsAsync(pushers);

            _mockAdoApi.Setup(m => m.GetLastPushDate(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(lastPushDate);
            _mockAdoApi.Setup(m => m.GetCommitCountSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, DateTime.Today.AddYears(-1))).ReturnsAsync(commitCount);

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"{ADO_TEAM_PROJECT}\",\"{ADO_REPO}\",\"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_git/{ADO_REPO}\",{pipelineCount},{prCount},\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",{commitCount},\"Arin\"{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Filter_Out_ActiveContributor_With_Service_In_The_Name()
        {
            // Arrange
            var pipelineCount = 41;
            var prCount = 822;
            var lastPushDate = DateTime.Now;
            var commitCount = 183;
            var pushers = new List<string>() { "BuildServiceAccount", "BuildServiceAccount", "Max" };

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspectorService.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(pipelineCount);
            _mockAdoInspectorService.Setup(m => m.GetPullRequestCount(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(prCount);
            _mockAdoApi.Setup(m => m.GetPushersSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, It.IsAny<DateTime>())).ReturnsAsync(pushers);

            _mockAdoApi.Setup(m => m.GetLastPushDate(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(lastPushDate);
            _mockAdoApi.Setup(m => m.GetCommitCountSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, DateTime.Today.AddYears(-1))).ReturnsAsync(commitCount);

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"{ADO_TEAM_PROJECT}\",\"{ADO_REPO}\",\"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_git/{ADO_REPO}\",{pipelineCount},{prCount},\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",{commitCount},\"Max\"{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Use_Pat_When_Passed()
        {
            var adoPat = Guid.NewGuid().ToString();

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _service.Generate(adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
