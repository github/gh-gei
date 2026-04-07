using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.GitlabToGithub;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands
{
    public class ReposCsvGeneratorServiceTests
    {
        private const string FULL_CSV_HEADER = "project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes,is-archived,pr-count";
        private const string MINIMAL_CSV_HEADER = "project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes";

        private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
        private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
        private readonly Mock<GitlabInspectorService> _mockGitlabInspectorService = TestHelpers.CreateMock<GitlabInspectorService>();
        private readonly Mock<GitlabInspectorServiceFactory> _mockGitlabInspectorServiceFactory = TestHelpers.CreateMock<GitlabInspectorServiceFactory>();

        private const string BBS_SERVER_URL = "http://bbs-server-url";
        private const string BBS_FOO_PROJECT = "project";
        private const string BBS_FOO_PROJECT_KEY = "FP";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const bool NO_SSL_VERIFY = true;
        private readonly (string, string) _bbsProject = (BBS_FOO_PROJECT_KEY, BBS_FOO_PROJECT);
        private const string BBS_REPO = "foo-repo";
        private const string BBS_REPO_SLUG = "foo-repo-slug";
        private const bool ARCHIVED = false;
        private const ulong REPO_SIZE = 10000UL;
        private const ulong ATTACHMENTS_SIZE = 10000UL;
        private readonly IEnumerable<GitlabRepository> _bbsRepos = [new() { Name = BBS_REPO, Slug = BBS_REPO_SLUG }];

        private readonly ReposCsvGeneratorService _service;

        public ReposCsvGeneratorServiceTests()
        {
            _mockGitlabInspectorServiceFactory.Setup(m => m.Create(_mockGitlabApi.Object)).Returns(_mockGitlabInspectorService.Object);
            _service = new ReposCsvGeneratorService(_mockGitlabInspectorServiceFactory.Object, _mockGitlabApiFactory.Object);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Repo()
        {
            // Arrange
            var prCount = 822;
            var lastCommitDate = DateTime.Now;

            _mockGitlabApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);

            _mockGitlabInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsProject);
            _mockGitlabInspectorService.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsRepos);
            _mockGitlabInspectorService.Setup(m => m.GetRepositoryPullRequestCount(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(prCount);
            _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(lastCommitDate);
            _mockGitlabApi.Setup(m => m.GetIsRepositoryArchived(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(ARCHIVED);
            _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG, BBS_USERNAME, BBS_PASSWORD)).ReturnsAsync((REPO_SIZE, ATTACHMENTS_SIZE));

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{BBS_FOO_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}/repos/{BBS_REPO_SLUG}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{REPO_SIZE:D}\",\"{ATTACHMENTS_SIZE:D}\",\"False\",{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Correct_Csv_For_One_Repo_Without_Archived_Field_For_Outdated_BBS_Version()
        {
            // Arrange
            var prCount = 822;
            var lastCommitDate = DateTime.Now;

            _mockGitlabApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);

            _mockGitlabInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsProject);
            _mockGitlabInspectorService.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsRepos);
            _mockGitlabInspectorService.Setup(m => m.GetRepositoryPullRequestCount(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(prCount);
            _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(lastCommitDate);
            _mockGitlabApi.Setup(m => m.GetIsRepositoryArchived(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(ARCHIVED);
            _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG, BBS_USERNAME, BBS_PASSWORD)).ReturnsAsync((REPO_SIZE, ATTACHMENTS_SIZE));

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY);

            // Assert
            var expected = $"{FULL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{BBS_FOO_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}/repos/{BBS_REPO_SLUG}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{REPO_SIZE:D}\",\"{ATTACHMENTS_SIZE:D}\",\"False\",{prCount}{Environment.NewLine}";

            result.Should().Be(expected);
        }

        [Fact]
        public async Task Generate_Should_Return_Minimal_Csv_When_Minimal_Is_True()
        {
            // Arrange
            var lastCommitDate = DateTime.Now;
            const bool minimal = true;

            _mockGitlabApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);

            _mockGitlabInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsProject);
            _mockGitlabInspectorService.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(_bbsRepos);
            _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(lastCommitDate);
            _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG, BBS_USERNAME, BBS_PASSWORD)).ReturnsAsync((REPO_SIZE, ATTACHMENTS_SIZE));

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY, minimal);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{BBS_FOO_PROJECT}\",\"{BBS_REPO}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}/repos/{BBS_REPO_SLUG}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{REPO_SIZE:D}\",\"{ATTACHMENTS_SIZE:D}\"{Environment.NewLine}";

            result.Should().Be(expected);
            _mockGitlabInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Generate_Should_Include_Empty_Entry_For_Null_Latest_Commit_Date()
        {
            // Arrange
            const bool minimal = true;

            var project_name = "project,name";
            var repo_name = "repo,name";
            var expected_project_name = "project%2Cname";
            var expected_repo_name = "repo%2Cname";
            var bbsProject = (BBS_FOO_PROJECT_KEY, project_name);
            var bbsRepos = new List<GitlabRepository> { new() { Name = repo_name, Slug = BBS_REPO_SLUG } };

            _mockGitlabApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);

            _mockGitlabInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(bbsProject);
            _mockGitlabInspectorService.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(bbsRepos);
            _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG));
            _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG, BBS_USERNAME, BBS_PASSWORD)).ReturnsAsync((REPO_SIZE, ATTACHMENTS_SIZE));

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY, minimal);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{expected_project_name}\",\"{expected_repo_name}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}/repos/{BBS_REPO_SLUG}\",,\"{REPO_SIZE:D}\",\"{ATTACHMENTS_SIZE:D}\"{Environment.NewLine}";

            result.Should().Be(expected);
            _mockGitlabInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Generate_Should_Escape_Project_And_Repo_Names()
        {
            // Arrange
            var lastCommitDate = DateTime.Now;
            const bool minimal = true;

            var project_name = "project,name";
            var repo_name = "repo,name";
            var expected_project_name = "project%2Cname";
            var expected_repo_name = "repo%2Cname";
            var bbsProject = (BBS_FOO_PROJECT_KEY, project_name);
            var bbsRepos = new List<GitlabRepository> { new() { Name = repo_name, Slug = BBS_REPO_SLUG } };

            _mockGitlabApiFactory.Setup(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);

            _mockGitlabInspectorService.Setup(m => m.GetProject(BBS_FOO_PROJECT_KEY)).ReturnsAsync(bbsProject);
            _mockGitlabInspectorService.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(bbsRepos);
            _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG)).ReturnsAsync(lastCommitDate);
            _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(BBS_FOO_PROJECT_KEY, BBS_REPO_SLUG, BBS_USERNAME, BBS_PASSWORD)).ReturnsAsync((REPO_SIZE, ATTACHMENTS_SIZE));

            // Act
            var result = await _service.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_FOO_PROJECT_KEY, minimal);

            // Assert
            var expected = $"{MINIMAL_CSV_HEADER}{Environment.NewLine}";
            expected += $"\"{BBS_FOO_PROJECT_KEY}\",\"{expected_project_name}\",\"{expected_repo_name}\",\"{BBS_SERVER_URL.TrimEnd('/')}/projects/{BBS_FOO_PROJECT_KEY}/repos/{BBS_REPO_SLUG}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{REPO_SIZE:D}\",\"{ATTACHMENTS_SIZE:D}\"{Environment.NewLine}";

            result.Should().Be(expected);
            _mockGitlabInspectorService.Verify(m => m.GetPullRequestCount(It.IsAny<string>()), Times.Never);
        }
    }
}
