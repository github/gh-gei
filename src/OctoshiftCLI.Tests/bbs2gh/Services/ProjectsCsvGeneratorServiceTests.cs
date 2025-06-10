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
        private const string FULL_CSV_HEADER = "project-key,project-name,url,repo-count,pr-count";
        private const string MINIMAL_CSV_HEADER = "project-key,project-name,url,repo-count";

        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly Mock<BbsApiFactory> _mockBbsApiFactory = TestHelpers.CreateMock<BbsApiFactory>();
        private readonly Mock<BbsInspectorService> _mockBbsInspectorService = TestHelpers.CreateMock<BbsInspectorService>();
        private readonly Mock<BbsInspectorServiceFactory> _mockBbsInspectorServiceFactory = TestHelpers.CreateMock<BbsInspectorServiceFactory>();

        private const string BBS_SERVER_URL = "http://bbs-server-url";
        private const string BBS_FOO_PROJECT = "project1";
        private const string BBS_BAR_PROJECT = "project2";
        private const string BBS_FOO_PROJECT_KEY = "FP";
        private const string BBS_BAR_PROJECT_KEY = "BP";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const bool NO_SSL_VERIFY = true;
        private readonly (string, string) _bbsProject = (BBS_FOO_PROJECT_KEY, BBS_FOO_PROJECT);
        private readonly IEnumerable<(string, string)> _bbsProjects = [(BBS_FOO_PROJECT_KEY, BBS_FOO_PROJECT), (BBS_BAR_PROJECT_KEY, BBS_BAR_PROJECT)];

        private readonly ProjectsCsvGeneratorService _service;

        public ProjectsCsvGeneratorServiceTests()
        {
            _mockBbsInspectorServiceFactory.Setup(m => m.Create(_mockBbsApi.Object)).Returns(_mockBbsInspectorService.Object);
            _service = new ProjectsCsvGeneratorService(_mockBbsInspectorServiceFactory.Object, _mockBbsApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Project()
        {
            // Arrange
            var repoCount = 82;
            var prCount = 822;

            _mockBbsApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockBbsApi.Object);

            _mockBbsInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsProject);
            _mockBbsInspectorService.Setup(m => m.GetRepoCount(BBS_FOO_PROJECT_KEY)).ReturnsAsync(repoCount);
            _mockBbsInspectorService.Setup(m => m.GetPullRequestCount(BBS_FOO_PROJECT_KEY)).ReturnsAsync(prCount);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";

            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{BBS_FOO_PROJECT}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}\",{repoCount},{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Is_True()
        {
            // Arrange
            const int repoCount1 = 82;
            const int repoCount2 = 0;
            const bool minimal = true;

            _mockBbsApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockBbsApi.Object);

            _mockBbsInspectorService.Setup(m => m.GetProjects()).ReturnsAsync(_bbsProjects);
            _mockBbsInspectorService.Setup(m => m.GetRepoCount(BBS_FOO_PROJECT_KEY)).ReturnsAsync(repoCount1);
            _mockBbsInspectorService.Setup(m => m.GetRepoCount(BBS_BAR_PROJECT_KEY)).ReturnsAsync(repoCount2);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, "", minimal);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{BBS_FOO_PROJECT}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}\",{repoCount1}{Environment.NewLine}";
            expected += $"\"{BBS_BAR_PROJECT_KEY}\",\"{BBS_BAR_PROJECT}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_BAR_PROJECT_KEY}\",{repoCount2}{Environment.NewLine}";

            result.Should().Be(expected);
            _mockBbsInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }
    }
}
