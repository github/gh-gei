using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.ReclaimMannequin;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();
    private readonly Mock<ConfirmationService> _mockConfirmationService = TestHelpers.CreateMock<ConfirmationService>();
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly ReclaimMannequinCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string MANNEQUIN_USER = "mona";
    private const string TARGET_USER = "mona_emu";
    private readonly string TARGET_USER_LOGIN = "mona_gh";

    public ReclaimMannequinCommandHandlerTests()
    {
        _handler = new ReclaimMannequinCommandHandler(_mockOctoLogger.Object, _mockReclaimService.Object, _mockConfirmationService.Object, _mockGithubApi.Object)
        {
            FileExists = _ => true,
            GetFileContent = _ => Array.Empty<string>()
        };
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

        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false, false)).Returns(Task.FromResult(default(object)));

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            MannequinUser = MANNEQUIN_USER,
            MannequinId = mannequinUserId,
            TargetUser = TARGET_USER,
        };
        await _handler.Handle(args);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false, false), Times.Once);
    }

    [Fact]
    public async Task SingleReclaiming_WithIdSpecifiedHappy_Path()
    {
        var mannequinUserId = "monaid";

        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false, false)).Returns(Task.FromResult(default(object)));

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            MannequinUser = MANNEQUIN_USER,
            MannequinId = mannequinUserId,
            TargetUser = TARGET_USER,
        };
        await _handler.Handle(args);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false, false), Times.Once);
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
        var role = "admin";

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            Csv = "file.csv",
        };

        _mockConfirmationService.Setup(x => x.AskForConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _mockGithubApi.Setup(x => x.GetLoginName().Result).Returns(TARGET_USER_LOGIN);
        _mockGithubApi.Setup(x => x.GetOrgMembershipForUser(GITHUB_ORG, TARGET_USER_LOGIN).Result).Returns(role);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false, true), Times.Once);
    }

    [Fact]
    public async Task Skip_Invitation_No_Confirmation_With_NoPrompt_Arg()
    {
        // Arrange
        var role = "admin";

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            Csv = "file.csv",
            NoPrompt = true
        };

        _mockGithubApi.Setup(x => x.GetLoginName().Result).Returns(TARGET_USER_LOGIN);
        _mockGithubApi.Setup(x => x.GetOrgMembershipForUser(GITHUB_ORG, TARGET_USER_LOGIN).Result).Returns(role);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false, true), Times.Once);
        _mockConfirmationService.Verify(x => x.AskForConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReclaimMannequinsSkipInvitation_No_Admin_Throws_Error()
    {
        // Arrange
        var role = "member";

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            SkipInvitation = true,
            Csv = "file.csv",
        };

        _mockConfirmationService.Setup(x => x.AskForConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _mockGithubApi.Setup(x => x.GetLoginName().Result).Returns(TARGET_USER_LOGIN);
        _mockGithubApi.Setup(x => x.GetOrgMembershipForUser(GITHUB_ORG, TARGET_USER_LOGIN).Result).Returns(role);

        // Act
        var exception = await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
        exception.WithMessage($"User {TARGET_USER_LOGIN} is not an org admin and is not eligible to reclaim mannequins with the --skip-invitation feature.");

        // Assert
        _mockGithubApi.Verify(m => m.GetLoginName(), Times.Once);
        _mockGithubApi.Verify(x => x.GetOrgMembershipForUser(GITHUB_ORG, TARGET_USER_LOGIN), Times.Once);
        _mockGithubApi.VerifyNoOtherCalls();
    }
}
