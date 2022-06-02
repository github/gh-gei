using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class PipelinesCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "org,teamproject,repo,pipeline,url";
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
        private const string ADO_PIPELINE = "foo-pipeline";
        private readonly IEnumerable<string> ADO_PIPELINES = new List<string>() { ADO_PIPELINE };

        private readonly PipelinesCsvGeneratorService _service;

        public PipelinesCsvGeneratorServiceTests()
        {
            _mockAdoInspectorServiceFactory.Setup(m => m.Create(_mockAdoApi.Object)).Returns(_mockAdoInspectorService.Object);
            _service = new PipelinesCsvGeneratorService(_mockAdoInspectorServiceFactory.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Pipeline()
        {
            // Arrange
            var pipelineId = 123;

            _mockAdoApi.Setup(m => m.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE)).ReturnsAsync(pipelineId);
            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(ADO_TEAM_PROJECTS);
            _mockAdoInspectorService.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(ADO_REPOS);
            _mockAdoInspectorService.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(ADO_PIPELINES);

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"{ADO_TEAM_PROJECT}\",\"{ADO_REPO}\",\"{ADO_PIPELINE}\",\"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_build?definitionId={pipelineId}\"{Environment.NewLine}";

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
