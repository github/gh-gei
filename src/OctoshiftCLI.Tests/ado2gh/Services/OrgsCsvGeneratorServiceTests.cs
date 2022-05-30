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
        private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();

        private const string ADO_ORG = "foo-org";
        private readonly IEnumerable<string> ADO_ORGS = new List<string>() { ADO_ORG };

        private readonly OrgsCsvGeneratorService _service;

        public OrgsCsvGeneratorServiceTests()
        {
            _service = new OrgsCsvGeneratorService(_mockAdoInspectorService.Object);
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

            _mockAdoInspectorService.Setup(m => m.GetOrgs()).ReturnsAsync(ADO_ORGS);
            _mockAdoInspectorService.Setup(m => m.GetTeamProjectCount(ADO_ORG)).ReturnsAsync(projectCount);
            _mockAdoInspectorService.Setup(m => m.GetRepoCount(ADO_ORG)).ReturnsAsync(repoCount);
            _mockAdoInspectorService.Setup(m => m.GetPipelineCount(ADO_ORG)).ReturnsAsync(pipelineCount);
            _mockAdoInspectorService.Setup(m => m.GetPullRequestCount(ADO_ORG)).ReturnsAsync(prCount);

            _mockAdoApi.Setup(m => m.GetOrgOwner(ADO_ORG)).ReturnsAsync(owner);

            // Act
            var result = await _service.Generate(_mockAdoApi.Object);

            // Assert
            var expected = $"{CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{ADO_ORG}\",\"https://dev.azure.com/{ADO_ORG}\",\"{owner}\",{projectCount},{repoCount},{pipelineCount},{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Throw_Exception_When_Passed_Null_AdoApi()
        {
            await FluentActions
                .Invoking(async () => await _service.Generate(null))
                .Should()
                .ThrowAsync<ArgumentNullException>();
        }
    }
}
