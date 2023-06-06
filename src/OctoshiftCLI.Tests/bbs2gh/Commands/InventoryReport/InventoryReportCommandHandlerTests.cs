using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Commands.InventoryReport;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandlerTests
{
    private const string BBS_SERVER_URL = "http://bbs-server-url";
    private const string BBS_PROJECT = "foo-project";
    private const string BBS_USERNAME = "bbs-username";
    private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
    private readonly Mock<BbsInspectorService> _mockBbsInspectorService = TestHelpers.CreateMock<BbsInspectorService>();
    private readonly Mock<ProjectsCsvGeneratorService> _mockProjectsCsvGenerator = TestHelpers.CreateMock<ProjectsCsvGeneratorService>();
    private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGenerator = TestHelpers.CreateMock<ReposCsvGeneratorService>();

    private string _projectsCsvOutput = "";
    private string _reposCsvOutput = "";

    private readonly InventoryReportCommandHandler _handler;

    public InventoryReportCommandHandlerTests()
    {
        _handler = new InventoryReportCommandHandler(
            TestHelpers.CreateMock<OctoLogger>().Object,
            _mockBbsApi.Object,
            _mockBbsInspectorService.Object,
            _mockProjectsCsvGenerator.Object,
            _mockReposCsvGenerator.Object)
        {
            WriteToFile = (path, contents) =>
            {
                if (path == "projects.csv")
                {
                    _projectsCsvOutput = contents;
                }

                if (path == "repos.csv")
                {
                    _reposCsvOutput = contents;
                }

                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task Happy_Path()
    {
        var expectedProjectsCsv = "csv stuff";
        var expectedReposCsv = "repo csv stuff";

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, BBS_USERNAME, false)).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, false)).ReturnsAsync(expectedReposCsv);

        var args = new InventoryReportCommandArgs();
        await _handler.Handle(args);

        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
    }

    [Fact]
    public async Task Scoped_To_Single_Project()
    {
        var expectedProjectsCsv = "csv stuff";
        var expectedReposCsv = "repo csv stuff";

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, BBS_USERNAME, false)).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, false)).ReturnsAsync(expectedReposCsv);

        var args = new InventoryReportCommandArgs
        {
            BbsProject = BBS_PROJECT,
        };
        await _handler.Handle(args);

        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);

        _mockBbsInspectorService.Object.ProjectFilter.Should().Be(BBS_PROJECT);
    }

    [Fact]
    public async Task It_Generates_Minimal_Csvs_When_Requested()
    {
        // Arrange
        var expectedProjectsCsv = "csv stuff";
        var expectedReposCsv = "repo csv stuff";

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, BBS_USERNAME, It.IsAny<bool>())).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, It.IsAny<bool>())).ReturnsAsync(expectedReposCsv);

        // Act
        var args = new InventoryReportCommandArgs
        {
            Minimal = true,
        };
        await _handler.Handle(args);

        // Assert
        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);

        _mockProjectsCsvGenerator.Verify(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, BBS_USERNAME, true));
        _mockReposCsvGenerator.Verify(m => m.Generate(BBS_SERVER_URL, BBS_PROJECT, true));
    }
}
