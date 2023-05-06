using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.AddTeamToRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.AddTeamToRepo;

public class AddTeamToRepoCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly AddTeamToRepoCommandHandler _handler;

    private readonly string GITHUB_ORG = "foo-org";
    private readonly string GITHUB_REPO = "foo-repo";
    private readonly string TEAM = "foo-team";

    public AddTeamToRepoCommandHandlerTests()
    {
        _handler = new AddTeamToRepoCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var teamSlug = "foo-slug";
        var role = "maintain";

        _mockGithubApi.Setup(x => x.GetTeamSlug(GITHUB_ORG, TEAM)).ReturnsAsync(teamSlug);

        var args = new AddTeamToRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            Team = TEAM,
            Role = role
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.AddTeamToRepo(GITHUB_ORG, GITHUB_REPO, teamSlug, role));
    }
}
