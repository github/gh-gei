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
    public class OrgsCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin,pr-count";
        private const string MINIMAL_CSV_HEADER = "name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin";

        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();

        private const string ADO_ORG = "foo-org";
        private readonly IEnumerable<string> _adoOrgs = [ADO_ORG];

        private readonly OrgsCsvGeneratorService _service;

        public OrgsCsvGeneratorServiceTests()
        {
            _mockAdoInspectorServiceFactory.Setup(m => m.Create(_mockAdoApi.Object)).Returns(_mockAdoInspectorService.Object);
            _service = new OrgsCsvGeneratorService(_mockAdoInspectorServiceFactory.Object, _mockAdoApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Org()
        {
            // Arrange
            var projectCount = 11;
            var repoCount = 82;
            var pipelineCount = 41;
            var prCount = 822;
            var owner = "Suzy (suzy@gmail.com)";

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(_adoOrgs);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjectCount(ADO_ORG)).ReturnsAsync(projectCount);
            _mockAdoInspectorService.Setup(m => m.GetRepoCount(ADO_ORG)).ReturnsAsync(repoCount);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG)).ReturnsAsync(pipelineCount);
            _mockAdoInspectorService.Setup(m => m.GetPullRequestCount(ADO_ORG)).ReturnsAsync(prCount);

            _mockAdoApi.Setup(m => m.GetOrgOwner(ADO_ORG)).ReturnsAsync(owner);
            _mockAdoApi.Setup(m => m.IsCallerOrgAdmin(ADO_ORG)).ReturnsAsync(true);

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"https://dev.azure.com/{ADO_ORG}\",\"{owner}\",{projectCount},{repoCount},{pipelineCount},{true},{prCount}{Environment.NewLine}";

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
            const string owner = "Suzy (suzy@gmail.com)";
            const int projectCount = 11;
            const int repoCount = 82;
            const int pipelineCount = 41;

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);
            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(_adoOrgs);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjectCount(ADO_ORG)).ReturnsAsync(projectCount);
            _mockAdoInspectorService.Setup(m => m.GetRepoCount(ADO_ORG)).ReturnsAsync(repoCount);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG)).ReturnsAsync(pipelineCount);

            _mockAdoApi.Setup(m => m.GetOrgOwner(ADO_ORG)).ReturnsAsync(owner);
            _mockAdoApi.Setup(m => m.IsCallerOrgAdmin(ADO_ORG)).ReturnsAsync(true);

            // Act
            var result = await _service.Generate(null, true);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"https://dev.azure.com/{ADO_ORG}\",\"{owner}\",{projectCount},{repoCount},{pipelineCount},{true}{Environment.NewLine}";

            result.Should().Be(expected);
            _mockAdoInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }
    }
}
