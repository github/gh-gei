using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class ReclaimMannequinCommandBaseTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();
    private readonly ReclaimMannequinCommandBase _command;

    private const string GITHUB_ORG = "FooOrg";
    private const string MANNEQUIN_USER = "mona";
    private const string TARGET_USER = "mona_emu";

    public ReclaimMannequinCommandBaseTests()
    {
        _command = new ReclaimMannequinCommandBase(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object, _mockReclaimService.Object)
        {
            FileExists = _ => true,
            GetFileContent = _ => Array.Empty<string>()
        };
    }

    [Fact]
    public async Task No_Parameters_Provided_Throws_OctoshiftCliException()
    {
        await FluentActions
            .Invoking(async () => await _command.Handle(GITHUB_ORG, null, null, null, null))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task It_Uses_The_Github_Pat_When_Provided()
    {
        string mannequinUserId = null;
        var githubTargetPat = "PAT";

        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));
        _mockTargetGithubApiFactory.Setup(m => m.Create(null, githubTargetPat)).Returns(_mockGithubApi.Object);

        await _command.Handle(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER, false, githubTargetPat);

        _mockTargetGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
    }

    [Fact]
    public async Task CSV_CSVFileDoesNotExist_OctoshiftCliException()
    {
        _command.FileExists = _ => false;

        await FluentActions
            .Invoking(async () => await _command.Handle("dummy", "I_DO_NOT_EXIST_CSV_PATH", null, null, null))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task SingleReclaiming_Happy_Path()
    {
        string mannequinUserId = null;

        _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

        await _command.Handle(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
    }

    [Fact]
    public async Task SingleReclaiming_WithIdSpecifiedHappy_Path()
    {
        var mannequinUserId = "monaid";

        _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
        _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

        await _command.Handle(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER);

        _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
    }

    [Fact]
    public async Task CSVReclaiming_Happy_Path()
    {
        _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);

        await _command.Handle(GITHUB_ORG, "file.csv", null, null, null);

        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false), Times.Once);
    }

    [Fact]
    public async Task CSV_CSV_TakesPrecedence()
    {
        _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);

        await _command.Handle(GITHUB_ORG, "file.csv", MANNEQUIN_USER, null, TARGET_USER); // All parameters passed. CSV has precedence

        _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false), Times.Once);
    }
}
