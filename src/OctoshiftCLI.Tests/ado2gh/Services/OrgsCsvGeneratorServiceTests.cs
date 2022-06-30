using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class OrgsCsvGeneratorServiceTests
    {
        private const string CSV_HEADER = "name,url,owner,teamproject-count,repo-count,pipeline-count,pr-count";
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();

        private const string ADO_ORG = "foo-org";
        private readonly IEnumerable<string> _adoOrgs = new List<string>() { ADO_ORG };

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

            // Act
            var result = await _service.Generate(null);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"https://dev.azure.com/{ADO_ORG}\",\"{owner}\",{projectCount},{repoCount},{pipelineCount},{prCount}{Environment.NewLine}";

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
