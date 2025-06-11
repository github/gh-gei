using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class TeamProjectsCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "org,teamproject,url,repo-count,pipeline-count,pr-count";
        private const string MINIMAL_CSV_HEADER = "org,teamproject,url,repo-count,pipeline-count";

        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();

        private const string ADO_ORG = "foo-org";
        private readonly IEnumerable<string> _adoOrgs = [ADO_ORG];
        private const string ADO_TEAM_PROJECT = "foo-tp";
        private readonly IEnumerable<string> _adoTeamProjects = [ADO_TEAM_PROJECT];

        private readonly TeamProjectsCsvGeneratorService _service;

        public TeamProjectsCsvGeneratorServiceTests()
        {
            _mockAdoInspectorServiceFactory.Setup(m => m.Create(_mockAdoApi.Object)).Returns(_mockAdoInspectorService.Object);
            _service = new TeamProjectsCsvGeneratorService(_mockAdoInspectorServiceFactory.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Team_Project()
        {
            // Arrange
            var repoCount = 82;
            var pipelineCount = 41;
            var prCount = 822;

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(_adoOrgs);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(_adoTeamProjects);
            _mockAdoInspectorService.Setup(m => m.GetRepoCount(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(repoCount);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(pipelineCount);
            _mockAdoInspectorService.Setup(m => m.GetPullRequestCount(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"{ADO_TEAM_PROJECT}\",\"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}\",{repoCount},{pipelineCount},{prCount}{Environment.NewLine}";

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

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Is_True()
        {
            // Arrange
            const int repoCount = 82;
            const int pipelineCount = 41;

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(_adoOrgs);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(_adoTeamProjects);
            _mockAdoInspectorService.Setup(m => m.GetRepoCount(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(repoCount);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(pipelineCount);

            // Act
            var result = await _service.Generate(null, true);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"{ADO_TEAM_PROJECT}\",\"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}\",{repoCount},{pipelineCount}{Environment.NewLine}";

            result.Should().Be(expected);
            _mockAdoInspectorService.Verify(m => m.GetPullRequestCount(ADO_ORG, ADO_TEAM_PROJECT), Times.Never);
        }
    }
}
