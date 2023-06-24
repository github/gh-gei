using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Commands.InventoryReport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.InventoryReport;

public class InventoryReportCommandHandlerTests
{
    private const string BBS_SERVER_URL = "http://bbs-server-url";
    private const string BBS_PROJECT_KEY = "FP";
    private const string BBS_PROJECT = "foo-project";
    private const string BBS_USERNAME = "bbs-username";
    private const string BBS_PASSWORD = "bbs-password";
    private const bool NO_SSL_VERIFY = true;
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

        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[] { (Id: 1, Key: BBS_PROJECT_KEY, Name: BBS_PROJECT) });
        _mockBbsInspectorService.Setup(m => m.GetRepoCount()).ReturnsAsync(1);

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, false)).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, false)).ReturnsAsync(expectedReposCsv);

        // var args = new InventoryReportCommandArgs();
        var args = new InventoryReportCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            NoSslVerify = NO_SSL_VERIFY
        };
        await _handler.Handle(args);

        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
    }

    [Fact]
    public async Task Scoped_To_Single_Project()
    {
        var expectedProjectsCsv = "csv stuff";
        var expectedReposCsv = "repo csv stuff";

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_PROJECT, false)).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, BBS_PROJECT, false)).ReturnsAsync(expectedReposCsv);

        var args = new InventoryReportCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsProject = BBS_PROJECT,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            NoSslVerify = NO_SSL_VERIFY
        };
        await _handler.Handle(args);

        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);
    }

    [Fact]
    public async Task It_Generates_Minimal_Csvs_When_Requested()
    {
        // Arrange
        var expectedProjectsCsv = "csv stuff";
        var expectedReposCsv = "repo csv stuff";

        _mockProjectsCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, It.IsAny<bool>())).ReturnsAsync(expectedProjectsCsv);
        _mockReposCsvGenerator.Setup(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, It.IsAny<bool>())).ReturnsAsync(expectedReposCsv);

        // Act
        var args = new InventoryReportCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            NoSslVerify = NO_SSL_VERIFY,
            Minimal = true
        };
        await _handler.Handle(args);

        // Assert
        _projectsCsvOutput.Should().Be(expectedProjectsCsv);
        _reposCsvOutput.Should().Be(expectedReposCsv);

        _mockProjectsCsvGenerator.Verify(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, true));
        _mockReposCsvGenerator.Verify(m => m.Generate(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, NO_SSL_VERIFY, null, true));
    }
}
