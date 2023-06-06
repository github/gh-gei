using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands
{
    public class BbsInspectorServiceTests
    {
        private readonly OctoLogger _logger = TestHelpers.CreateMock<OctoLogger>().Object;
        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly BbsInspectorService _service;

        private const string BBS_PROJECT = "BBS_PROJECT";
        private const string FOO_REPO = "FOO_REPO";
        private const string BBS_FOO_PROJECT_KEY = "FP";
         private const string BBS_FOO_REPO_1_SLUG = "foorepo1";
          private const string BBS_FOO_REPO_2_SLUG = "foorepo2";

        public BbsInspectorServiceTests() => _service = new(_logger, _mockBbsApi.Object);

        [Fact]
        public async Task GetProjects_Should_Return_All_Projects()
        {
            // Arrange
            var project1 = "my-project";
            var project2 = "other-project";
            var projects = new[] {
                (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: project1),
                (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: project2)
            };
            var userId = Guid.NewGuid().ToString();

            _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(projects);

            // Act
            var result = await _service.GetProjects();

            // Assert
            result.Should().BeEquivalentTo(projects);
        }

        [Fact]
        public async Task GetProjects_Should_Return_Single_Project_Collection_When_Project_Passed()
        {
            // Arrange
            _service.ProjectFilter = BBS_PROJECT;

            // Act
            var result = await _service.GetProjects();

            // Assert
            result.Count().Should().Be(1);
            result.First().Should().Be(BBS_PROJECT);

            _mockBbsApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetRepos_Should_Return_All_Repos()
        {
            // Arrange
            var repos = new List<BbsRepository> { new() { Name = "repo1" }, new() { Name = "repo2" } };

            _mockBbsApi.Setup(m => m.GetRepos(BBS_PROJECT));

            // Act
            var result = await _service.GetRepos(BBS_PROJECT);

            // Assert
            result.Should().BeEquivalentTo(repos);
        }

        [Fact]
        public async Task LoadReposCsv_Should_Set_Projects()
        {
            // Arrange
            var csvPath = "repos.csv";
            var csvContents = $"project,repo{Environment.NewLine}\"{BBS_PROJECT}\",\"{FOO_REPO}\"";

            _service.OpenFileStream = _ => csvContents.ToStream();

            // Act
            _service.LoadReposCsv(csvPath);

            // Assert
            (await _service.GetProjects()).Should().BeEquivalentTo(new List<string>() { BBS_PROJECT });
        }

        [Fact]
        public async Task LoadReposCsv_Should_Set_Repos()
        {
            // Arrange
            var csvPath = "repos.csv";
            var csvContents = $"project,repo{Environment.NewLine}\"{BBS_PROJECT}\",\"{FOO_REPO}\"";

            _service.OpenFileStream = _ => csvContents.ToStream();

            // Act
            _service.LoadReposCsv(csvPath);

            // Assert
            (await _service.GetRepos(BBS_PROJECT)).Single().Name.Should().Be(FOO_REPO);
        }
    }
}
