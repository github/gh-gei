using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
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
        private readonly IEnumerable<string> _adoOrgs = [ADO_ORG];
        private const string ADO_TEAM_PROJECT = "foo-tp";
        private readonly IEnumerable<string> _adoTeamProjects = [ADO_TEAM_PROJECT];
        private const string ADO_REPO = "foo-repo";
        private readonly IEnumerable<AdoRepository> _adoRepos = [new() { Name = ADO_REPO }];
        private const string ADO_PIPELINE = "foo-pipeline";
        private readonly IEnumerable<string> _adoPipelines = [ADO_PIPELINE];

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

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(_adoOrgs);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(_adoTeamProjects);
            _mockAdoInspectorService.Setup(m => m.GetRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(_adoRepos);
            _mockAdoInspectorService.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO)).ReturnsAsync(_adoPipelines);

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
