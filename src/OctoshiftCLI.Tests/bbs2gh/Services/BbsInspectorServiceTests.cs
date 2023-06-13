using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;
using Newtonsoft.Json.Linq;

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

            _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(projects);

            // Act
            var result = await _service.GetProjects();

            // Assert
            result.Should().BeEquivalentTo(new List<string>() { project1, project2 });
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
            var repo1 = "repo1";
            var repo2 = "repo2";
            var repos = new[]
            {
                (Id: 1, Slug: repo1, Name: repo1, Archived: false),
                (Id: 2, Slug: repo2, Name: repo2, Archived: false)
            };

            _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(repos);

            // Act
            var result = await _service.GetRepos(BBS_FOO_PROJECT_KEY);

            // Assert
            result.Should().BeEquivalentTo(new List<BbsRepository>() { 
                new() { Name = repo1, Archived = false },
                new() { Name = repo2, Archived = false }
            });
        }

        [Fact]
        public async Task GetRepoCount_Should_Return_Count()
        {
            // Arrange
            var project = "project";
            var projects = new[] {
                (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: project)
            };
            var repo1 = "repo1";
            var repo2 = "repo2";
            var repos = new[]
            {
                (Id: 1, Slug: repo1, Name: repo1, Archived: false),
                (Id: 2, Slug: repo2, Name: repo2, Archived: false)
            };
            var expectedCount = 2;

            _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(projects);
            _mockBbsApi.Setup(m => m.GetRepos(project)).ReturnsAsync(repos);

            // Act
            var result = await _service.GetRepoCount();

            // Assert
            result.Should().Be(expectedCount);
        }

        [Fact]
        public async Task GetPullRequestCount_Should_Return_Count()
        {
            // Arrange
            var project = "project";
            var projects = new[] {
                (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: project)
            };

            var repo1 = "repo1";
            var repo2 = "repo2";
            var repos = new[]
            {
                (Id: 1, Slug: repo1, Name: repo1, Archived: false),
                (Id: 2, Slug: repo2, Name: repo2, Archived: false)
            };

            var prs1 = new[]
            {
                (Id: 1, Name: "pr1"),
                (Id: 2, Name: "pr2")
            };
            var prs2 = new[]
            {
                (Id: 3, Name: "pr3")
            };
            var expectedCount = 3;

            _mockBbsApi.Setup(m => m.GetRepos(project)).ReturnsAsync(repos);
            _mockBbsApi.Setup(m => m.GetRepositoryPullRequests(project, repo1)).ReturnsAsync(prs1);
            _mockBbsApi.Setup(m => m.GetRepositoryPullRequests(project, repo2)).ReturnsAsync(prs2);

            // Act
            var result = await _service.GetPullRequestCount(project);

            // Assert
            result.Should().Be(expectedCount);
        }

        [Fact]
        public async Task GetRepositoryPullRequestCount_Should_Return_Count()
        {
            // Arrange
            var project = "project";
            var projects = new[] {
                (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: project)
            };

            var repo = "repo1";
            var prs = new[]
            {
                (Id: 1, Name: "pr1"),
                (Id: 2, Name: "pr2")
            };
            var expectedCount = 2;

            _mockBbsApi.Setup(m => m.GetRepositoryPullRequests(project, repo)).ReturnsAsync(prs);

            // Act
            var result = await _service.GetRepositoryPullRequestCount(project, repo);

            // Assert
            result.Should().Be(expectedCount);
        }

        [Fact]
        public async Task GetLastCommitDate_Should_Return_LastCommitDate()
        {
            var expectedDate = new DateTime(2022, 2, 14);

            var commit = new {
                values = new[]
                {
                    new
                    {
                        authorTimestamp = 1644816000000,
                    }
                }
            };
            var jObject = JObject.Parse(commit.ToJson());
            var response = Task.FromResult(jObject);

            _mockBbsApi.Setup(m => m.GetRepositoryLatestCommit(BBS_FOO_PROJECT_KEY, FOO_REPO)).Returns(response);

            var result = await _service.GetLastCommitDate(BBS_FOO_PROJECT_KEY, FOO_REPO);

            result.Should().Be(expectedDate);
        }

        [Fact]
        public async Task GetLastCommitDate_Should_Return_MinDate_When_No_Commits()
        {
            var commit = new {
                values = Array.Empty<object>()
            };
            var jObject = JObject.Parse(commit.ToJson());
            var response = Task.FromResult(jObject);

            _mockBbsApi.Setup(m => m.GetRepositoryLatestCommit(BBS_FOO_PROJECT_KEY, FOO_REPO)).Returns(response);

            var result = await _service.GetLastCommitDate(BBS_FOO_PROJECT_KEY, FOO_REPO);

            result.Should().Be(DateTime.MinValue);
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
