using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands
{
    public class ProjectsCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "name,url,repo-count,pr-count";
        private const string MINIMAL_CSV_HEADER = "name,url,repo-count";

        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly Mock<BbsInspectorService> _mockBbsInspectorService = TestHelpers.CreateMock<BbsInspectorService>();
        private readonly Mock<BbsInspectorServiceFactory> _mockBbsInspectorServiceFactory = TestHelpers.CreateMock<BbsInspectorServiceFactory>();

        private const string BBS_SERVER_URL = "http://bbs-server-url";
        private const string BBS_PROJECT = "foo-projects";
        private readonly IEnumerable<string> _bbsProjects = new List<string>() { BBS_PROJECT };

        private readonly ProjectsCsvGeneratorService _service;

        public ProjectsCsvGeneratorServiceTests()
        {
            _mockBbsInspectorServiceFactory.Setup(m => m.Create(_mockBbsApi.Object)).Returns(_mockBbsInspectorService.Object);
            _service = new ProjectsCsvGeneratorService(_mockBbsApi.Object, _mockBbsInspectorServiceFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Project()
        {
            // Arrange
            var repoCount = 82;
            var prCount = 822;

            _mockBbsInspectorService.Setup(m => m.GetRepoCount(BBS_PROJECT)).ReturnsAsync(repoCount);
            _mockBbsInspectorService.Setup(m => m.GetPullRequestCount(BBS_PROJECT)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_PROJECT);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";

            expected += $"\"{BBS_PROJECT}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_PROJECT}\",{repoCount},{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
            _mockBbsInspectorService.Verify(m => m.GetProjects(), Times.Never);
        }

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Is_True()
        {
            // Arrange
            const int repoCount = 82;

            _mockBbsInspectorService.Setup(m => m.GetProjects()).ReturnsAsync(_bbsProjects);
            _mockBbsInspectorService.Setup(m => m.GetRepoCount(BBS_PROJECT)).ReturnsAsync(repoCount);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_PROJECT, true);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_PROJECT}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_PROJECT}\",{repoCount}{Environment.NewLine}";

            result.Should().Be(expected);
            _mockBbsInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }
    }
}
