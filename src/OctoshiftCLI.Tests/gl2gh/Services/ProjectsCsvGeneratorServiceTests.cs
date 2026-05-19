using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.GitlabToGithub;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Services;

public class ProjectsCsvGeneratorServiceTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_PAT = "gitlab-pat";
    private const bool NO_SSL_VERIFY = true;

    private const string GROUP_PATH = "group-1";
    private const string GROUP_NAME = "Group 1";
    private const string PROJECT_PATH = "project-1";
    private const string PROJECT_NAME = "Project 1";

    private const string FULL_CSV_HEADER = "group-path,group-name,project,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes,is-archived,mr-count";
    private const string MINIMAL_CSV_HEADER = "group-path,group-name,project,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes,is-archived";

    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
    private readonly Mock<GitlabInspectorService> _mockGitlabInspectorService = TestHelpers.CreateMock<GitlabInspectorService>();
    private readonly Mock<GitlabInspectorServiceFactory> _mockGitlabInspectorServiceFactory = TestHelpers.CreateMock<GitlabInspectorServiceFactory>();

    private readonly ProjectsCsvGeneratorService _service;

    public ProjectsCsvGeneratorServiceTests()
    {
        _mockGitlabApiFactory.Setup(m => m.Create(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY)).Returns(_mockGitlabApi.Object);
        _mockGitlabInspectorServiceFactory.Setup(m => m.Create(_mockGitlabApi.Object)).Returns(_mockGitlabInspectorService.Object);
        _service = new ProjectsCsvGeneratorService(_mockGitlabInspectorServiceFactory.Object, _mockGitlabApiFactory.Object);
    }

    [Fact]
    public async Task Generate_Returns_Csv_For_Single_Group()
    {
        var lastCommitDate = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        const long repoSize = 1234;
        const long attachmentsSize = 5678;
        const int mrCount = 7;

        _mockGitlabInspectorService.Setup(m => m.GetGroup(GROUP_PATH)).ReturnsAsync((GROUP_PATH, GROUP_NAME));
        _mockGitlabInspectorService
            .Setup(m => m.GetProjects(GROUP_PATH))
            .ReturnsAsync(new[] { new GitlabProject { Name = PROJECT_NAME, Path = PROJECT_PATH } });
        _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(GROUP_PATH, PROJECT_PATH)).ReturnsAsync(lastCommitDate);
        _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(GROUP_PATH, PROJECT_PATH)).ReturnsAsync((repoSize, attachmentsSize));
        _mockGitlabInspectorService.Setup(m => m.GetProjectMergeRequestCount(GROUP_PATH, PROJECT_PATH)).ReturnsAsync(mrCount);

        var result = await _service.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, GROUP_PATH);

        var expected =
            $"{FULL_CSV_HEADER}{Environment.NewLine}" +
            $"\"{GROUP_PATH}\",\"{GROUP_NAME}\",\"{PROJECT_NAME}\",\"{GITLAB_SERVER_URL}/{GROUP_PATH}/{PROJECT_PATH}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{repoSize:D}\",\"{attachmentsSize:D}\",\"\",{mrCount}{Environment.NewLine}";

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Generate_Returns_Minimal_Csv_When_Requested()
    {
        const long repoSize = 1234;
        const long attachmentsSize = 5678;
        var lastCommitDate = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

        _mockGitlabInspectorService.Setup(m => m.GetGroup(GROUP_PATH)).ReturnsAsync((GROUP_PATH, GROUP_NAME));
        _mockGitlabInspectorService
            .Setup(m => m.GetProjects(GROUP_PATH))
            .ReturnsAsync(new[] { new GitlabProject { Name = PROJECT_NAME, Path = PROJECT_PATH } });
        _mockGitlabApi.Setup(m => m.GetRepositoryLatestCommitDate(GROUP_PATH, PROJECT_PATH)).ReturnsAsync(lastCommitDate);
        _mockGitlabApi.Setup(m => m.GetRepositoryAndAttachmentsSize(GROUP_PATH, PROJECT_PATH)).ReturnsAsync((repoSize, attachmentsSize));

        var result = await _service.Generate(GITLAB_SERVER_URL, GITLAB_PAT, NO_SSL_VERIFY, GROUP_PATH, minimal: true);

        var expected =
            $"{MINIMAL_CSV_HEADER}{Environment.NewLine}" +
            $"\"{GROUP_PATH}\",\"{GROUP_NAME}\",\"{PROJECT_NAME}\",\"{GITLAB_SERVER_URL}/{GROUP_PATH}/{PROJECT_PATH}\",\"{lastCommitDate:yyyy-MM-dd hh:mm tt}\",\"{repoSize:D}\",\"{attachmentsSize:D}\",\"\"{Environment.NewLine}";

        result.Should().Be(expected);
        _mockGitlabInspectorService.Verify(m => m.GetProjectMergeRequestCount(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
