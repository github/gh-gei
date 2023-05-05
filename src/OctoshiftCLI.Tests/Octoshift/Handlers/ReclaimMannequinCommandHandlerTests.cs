using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Handlers;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class ReclaimMannequinCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();
    private readonly ConfirmationService _confirmationService;
    private readonly ReclaimMannequinCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string MANNEQUIN_USER = "mona";
    private const string TARGET_USER = "mona_emu";

    private string _consoleOutput;

    public ReclaimMannequinCommandHandlerTests()
    {
        _confirmationService = new ConfirmationService(CaptureConsoleOutput, MockConsoleKeyPress);
        _handler = new ReclaimMannequinCommandHandler(_mockOctoLogger.Object, _mockReclaimService.Object, _confirmationService)
        {
            FileExists = _ => true,
            GetFileContent = _ => Array.Empty<string>()
        };
    }

    [Fact]
    public async Task No_Parameters_Provided_Throws_OctoshiftCliException()
    {
        var args = new ReclaimMannequinCommandArgs { GithubOrg = GITHUB_ORG };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task CSV_CSVFileDoesNotExist_OctoshiftCliException()
    {
        _handler.FileExists = _ => false;

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Csv = "I_DO_NOT_EXIST_CSV_PATH",
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task SingleReclaiming_Happy_Path()
    {
        string mannequinUserId = null;

        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            MannequinUser = MANNEQUIN_USER,
            MannequinId = mannequinUserId,
            TargetUser = TARGET_USER,
        };
        await _handler.Handle(args);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
    }

    [Fact]
    public async Task SingleReclaiming_WithIdSpecifiedHappy_Path()
    {
        var mannequinUserId = "monaid";

        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            MannequinUser = MANNEQUIN_USER,
            MannequinId = mannequinUserId,
            TargetUser = TARGET_USER,
        };
        await _handler.Handle(args);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
    }

    [Fact]
    public async Task CSVReclaiming_Happy_Path()
    {
        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Csv = "file.csv",
        };
        await _handler.Handle(args);

        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false, false), Times.Once);
    }

    [Fact]
    public async Task CSV_CSV_TakesPrecedence()
    {
        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Csv = "file.csv",
            MannequinUser = MANNEQUIN_USER,
            TargetUser = TARGET_USER,
        };
        await _handler.Handle(args); // All parameters passed. CSV has precedence

        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false, false), Times.Once);
    }

    [Fact]
    public async Task Skip_Invitation_Happy_Path()
    {
        // Arrange
        var expectedResult = "Skipping the reclaimation email invitation is irreversible. Are you sure you wish to continue? y\\nConfirmation Recorded. Proceeding...";

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            Csv = "file.csv",
        };

        // Act
        await _handler.Handle(args);

        // Assert
        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false, true), Times.Once);
        _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task Skip_Invitation_Without_CSV_Throws_Error()
    {
        // Arrange
        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            MannequinUser = MANNEQUIN_USER,
            TargetUser = TARGET_USER,
        };

        // Act/ Assert
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;

    private ConsoleKey MockConsoleKeyPress() => ConsoleKey.Y;
}
