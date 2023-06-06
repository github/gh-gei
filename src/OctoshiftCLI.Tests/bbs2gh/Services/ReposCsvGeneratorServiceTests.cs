using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "project,repo,url,last-push-date,compressed-repo-size-in-bytes,most-active-contributor,pr-count,commits-past-year";
        private const string MINIMAL_CSV_HEADER = "project,repo,url,last-push-date,compressed-repo-size-in-bytes";

        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly Mock<BbsInspectorService> _mockBbsInspectorService = TestHelpers.CreateMock<BbsInspectorService>();
        private readonly Mock<BbsInspectorServiceFactory> _mockBbsInspectorServiceFactory = TestHelpers.CreateMock<BbsInspectorServiceFactory>();

        private const string BBS_SERVER_URL = "http://bbs-server-url";
        private const string BBS_PROJECT = "foo-project";
        private readonly IEnumerable<string> _bbsProjects = new List<string>() { BBS_PROJECT };
        private const string BBS_REPO = "foo-repo";
        private readonly IEnumerable<BbsRepository> _bbsRepos = new List<BbsRepository> { new() { Name = BBS_REPO, Size = 12345 } };

        private readonly ReposCsvGeneratorService _service;

        public ReposCsvGeneratorServiceTests()
        {
            //_mockBbsApi.Setup();
            _mockBbsInspectorServiceFactory.Setup(m => m.Create(_mockBbsApi.Object)).Returns(_mockBbsInspectorService.Object);
            _service = new ReposCsvGeneratorService(_mockBbsApi.Object, _mockBbsInspectorServiceFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Repo()
        {
            // Arrange
            var prCount = 822;
            var lastPushDate = DateTime.Now;
            var pushers = new List<string>() { "Dylan", "Arin", "Arin", "Max" };

            _mockBbsInspectorService.Setup(m => m.GetProjects()).ReturnsAsync(_bbsProjects);
            _mockBbsInspectorService.Setup(m => m.GetRepos(BBS_PROJECT)).ReturnsAsync(_bbsRepos);
            _mockBbsInspectorService.Setup(m => m.GetPullRequestCount(BBS_PROJECT, BBS_REPO)).ReturnsAsync(prCount);
            // _mockBbsApi.Setup(m => m.GetPushersSince(BBS_PROJECT, BBS_REPO, It.IsAny<DateTime>())).ReturnsAsync(pushers);

            // _mockBbsApi.Setup(m => m.GetLastPushDate(BBS_PROJECT, BBS_REPO)).ReturnsAsync(lastPushDate);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_PROJECT);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_PROJECT}/repos/{BBS_REPO}\",\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",\"12,345\",\"Arin\",{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Filter_Out_ActiveContributor_With_Service_In_The_Name()
        {
            // Arrange
            var prCount = 822;
            var lastPushDate = DateTime.Now;
            var pushers = new List<string>() { "BuildServiceAccount", "BuildServiceAccount", "Max" };

            _mockBbsInspectorService.Setup(m => m.GetProjects()).ReturnsAsync(_bbsProjects);
            _mockBbsInspectorService.Setup(m => m.GetRepos(BBS_PROJECT)).ReturnsAsync(_bbsRepos);
            _mockBbsInspectorService.Setup(m => m.GetPullRequestCount(BBS_PROJECT, BBS_REPO)).ReturnsAsync(prCount);
            // _mockBbsApi.Setup(m => m.GetPushersSince(BBS_PROJECT, BBS_REPO, It.IsAny<DateTime>())).ReturnsAsync(pushers);

            // _mockBbsApi.Setup(m => m.GetLastPushDate(BBS_PROJECT, BBS_REPO)).ReturnsAsync(lastPushDate);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_PROJECT);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_PROJECT}/repos/{BBS_REPO}\",\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",\"12,345\",\"Max\",{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Is_True()
        {
            // Arrange
            var lastPushDate = DateTime.Now;

            // _mockBbsApiFactory.Setup(m => m.Create(null)).Returns(_mockBbsApi.Object);

            _mockBbsInspectorService.Setup(m => m.GetProjects()).ReturnsAsync(_bbsProjects);
            _mockBbsInspectorService.Setup(m => m.GetRepos(BBS_PROJECT)).ReturnsAsync(_bbsRepos);

            // _mockBbsApi.Setup(m => m.GetLastPushDate(BBS_PROJECT, BBS_REPO)).ReturnsAsync(lastPushDate);

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_PROJECT, true);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_PROJECT}/repos/{BBS_REPO}\",\"{lastPushDate:dd-MMM-yyyy hh:mm tt}\",\"12,345\"{Environment.NewLine}";

            result.Should().Be(expected);
            _mockBbsInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
            // _mockBbsApi.Verify(m => m.GetPushersSince(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }
    }
}
