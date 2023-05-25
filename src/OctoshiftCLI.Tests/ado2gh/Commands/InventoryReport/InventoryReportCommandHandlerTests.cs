using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands.InventoryReport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandlerTests
{
    private const string ADO_ORG = "foo-org";
    private readonly Mock<AdoInspectorService> _mockAdoInspectorService = TestHelpers.CreateMock<AdoInspectorService>();
    private readonly Mock<OrgsCsvGeneratorService> _mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
    private readonly Mock<TeamProjectsCsvGeneratorService> _mockTeamProjectsCsvGenerator = TestHelpers.CreateMock<TeamProjectsCsvGeneratorService>();
    private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGenerator = TestHelpers.CreateMock<ReposCsvGeneratorService>();
    private readonly Mock<PipelinesCsvGeneratorService> _mockPipelinesCsvGenerator = TestHelpers.CreateMock<PipelinesCsvGeneratorService>();

    private string _orgsCsvOutput = "";
    private string _teamProjectsCsvOutput = "";
    private string _reposCsvOutput = "";
    private string _pipelinesCsvOutput = "";

    private readonly InventoryReportCommandHandler _handler;

    public InventoryReportCommandHandlerTests()
    {
        _handler = new InventoryReportCommandHandler(
            TestHelpers.CreateMock<OctoLogger>().Object,
            _mockAdoInspectorService.Object,
            _mockOrgsCsvGenerator.Object,
            _mockTeamProjectsCsvGenerator.Object,
            _mockReposCsvGenerator.Object,
            _mockPipelinesCsvGenerator.Object)
        {
            WriteToFile = (path, contents) =>
            {
                if (path == "orgs.csv")
                {
                    _orgsCsvOutput = contents;
                }

                if (path == "team-projects.csv")
                {
                    _teamProjectsCsvOutput = contents;
                }

                if (path == "repos.csv")
                {
                    _reposCsvOutput = contents;
                }

                if (path == "pipelines.csv")
                {
                    _pipelinesCsvOutput = contents;
                }

                return Task.CompletedTask;
            }
        };
    }

    [Fact]
    public async Task Happy_Path()
    {
        var expectedOrgsCsv = "csv stuff";
        var expectedTeamProjectsCsv = "more csv stuff";
        var expectedReposCsv = "repo csv stuff";
        var expectedPipelinesCsv = "pipelines csv stuff";

        _mockOrgsCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedOrgsCsv);
        _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedTeamProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedReposCsv);
        _mockPipelinesCsvGenerator.Setup(m => m.Generate(null)).ReturnsAsync(expectedPipelinesCsv);

        var args = new InventoryReportCommandArgs();
        await _handler.Handle(args);

        _orgsCsvOutput.Should().Be(expectedOrgsCsv);
        _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
        _pipelinesCsvOutput.Should().Be(expectedPipelinesCsv);
    }

    [Fact]
    public async Task Scoped_To_Single_Org()
    {
        var expectedOrgsCsv = "csv stuff";
        var expectedTeamProjectsCsv = "more csv stuff";
        var expectedReposCsv = "repo csv stuff";
        var expectedPipelinesCsv = "pipelines csv stuff";

        _mockOrgsCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedOrgsCsv);
        _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedTeamProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(null, false)).ReturnsAsync(expectedReposCsv);
        _mockPipelinesCsvGenerator.Setup(m => m.Generate(null)).ReturnsAsync(expectedPipelinesCsv);

        var args = new InventoryReportCommandArgs
        {
            AdoOrg = ADO_ORG,
        };
        await _handler.Handle(args);

        _orgsCsvOutput.Should().Be(expectedOrgsCsv);
        _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
        _pipelinesCsvOutput.Should().Be(expectedPipelinesCsv);

        _mockAdoInspectorService.Object.OrgFilter.Should().Be(ADO_ORG);
    }

    [Fact]
    public async Task It_Generates_Minimal_Csvs_When_Requested()
    {
        // Arrange
        var expectedOrgsCsv = "csv stuff";
        var expectedTeamProjectsCsv = "more csv stuff";
        var expectedReposCsv = "repo csv stuff";
        var expectedPipelinesCsv = "pipelines csv stuff";

        _mockOrgsCsvGenerator.Setup(m => m.Generate(null, It.IsAny<bool>())).ReturnsAsync(expectedOrgsCsv);
        _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(null, It.IsAny<bool>())).ReturnsAsync(expectedTeamProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(null, It.IsAny<bool>())).ReturnsAsync(expectedReposCsv);
        _mockPipelinesCsvGenerator.Setup(m => m.Generate(null)).ReturnsAsync(expectedPipelinesCsv);

        // Act
        var args = new InventoryReportCommandArgs
        {
            Minimal = true,
        };
        await _handler.Handle(args);

        // Assert
        _orgsCsvOutput.Should().Be(expectedOrgsCsv);
        _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
        _pipelinesCsvOutput.Should().Be(expectedPipelinesCsv);

        _mockOrgsCsvGenerator.Verify(m => m.Generate(null, true));
        _mockTeamProjectsCsvGenerator.Verify(m => m.Generate(null, true));
        _mockReposCsvGenerator.Verify(m => m.Generate(null, true));
    }
}
