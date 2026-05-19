using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Services;

public class GitlabInspectorServiceTests
{
    private readonly OctoLogger _logger = TestHelpers.CreateMock<OctoLogger>().Object;
    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly GitlabInspectorService _service;

    private const string GROUP_PATH_1 = "group-1";
    private const string GROUP_NAME_1 = "Group 1";
    private const string GROUP_PATH_2 = "group-2";
    private const string GROUP_NAME_2 = "Group 2";

    public GitlabInspectorServiceTests() => _service = new(_logger, _mockGitlabApi.Object);

    [Fact]
    public async Task GetGroups_Returns_Path_And_Name()
    {
        _mockGitlabApi
            .Setup(m => m.GetGroups())
            .ReturnsAsync(new[]
            {
                (Id: 1L, Path: GROUP_PATH_1, Name: GROUP_NAME_1),
                (Id: 2L, Path: GROUP_PATH_2, Name: GROUP_NAME_2)
            });

        var result = await _service.GetGroups();

        result.Should().BeEquivalentTo([(GROUP_PATH_1, GROUP_NAME_1), (GROUP_PATH_2, GROUP_NAME_2)]);
    }

    [Fact]
    public async Task GetProjects_Returns_Projects_For_Group()
    {
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_1))
            .ReturnsAsync(new[]
            {
                (Id: 1L, Path: "project-1", Name: "Project 1", Archived: false),
                (Id: 2L, Path: "project-2", Name: "Project 2", Archived: true)
            });

        var result = (await _service.GetProjects(GROUP_PATH_1)).ToList();

        result.Should().HaveCount(2);
        result[0].Path.Should().Be("project-1");
        result[0].Name.Should().Be("Project 1");
        result[1].Path.Should().Be("project-2");
        result[1].Name.Should().Be("Project 2");
    }

    [Fact]
    public async Task GetProjectCount_For_Group_Returns_Count()
    {
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_1))
            .ReturnsAsync(new[]
            {
                (Id: 1L, Path: "p1", Name: "p1", Archived: false),
                (Id: 2L, Path: "p2", Name: "p2", Archived: false)
            });

        var result = await _service.GetProjectCount(GROUP_PATH_1);

        result.Should().Be(2);
    }

    [Fact]
    public async Task GetProjectCount_For_Multiple_Groups_Returns_Sum()
    {
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_1))
            .ReturnsAsync(new[] { (Id: 1L, Path: "p1", Name: "p1", Archived: false) });
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_2))
            .ReturnsAsync(new[]
            {
                (Id: 2L, Path: "p2", Name: "p2", Archived: false),
                (Id: 3L, Path: "p3", Name: "p3", Archived: false)
            });

        var result = await _service.GetProjectCount(new[] { GROUP_PATH_1, GROUP_PATH_2 });

        result.Should().Be(3);
    }

    [Fact]
    public async Task GetProjectMergeRequestCount_Returns_Count_From_Api()
    {
        _mockGitlabApi
            .Setup(m => m.GetMergeRequestCount(GROUP_PATH_1, "project-1"))
            .ReturnsAsync(7);

        var result = await _service.GetProjectMergeRequestCount(GROUP_PATH_1, "project-1");

        result.Should().Be(7);
    }
}
