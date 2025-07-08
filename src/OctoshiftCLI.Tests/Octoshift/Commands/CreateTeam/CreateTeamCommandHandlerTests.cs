using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.CreateTeam;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.CreateTeam;

public class CreateTeamCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly CreateTeamCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string TEAM_NAME = "foo-team";
    private const string IDP_GROUP = "foo-group";
    private readonly List<string> TEAM_MEMBERS = ["dylan", "dave"];
    private const int IDP_GROUP_ID = 42;
    private const string TEAM_SLUG = "foo-slug";

    public CreateTeamCommandHandlerTests()
    {
        _handler = new CreateTeamCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        _mockGithubApi.Setup(x => x.GetTeamMembers(GITHUB_ORG, TEAM_SLUG).Result).Returns(TEAM_MEMBERS);
        _mockGithubApi.Setup(x => x.GetIdpGroupId(GITHUB_ORG, IDP_GROUP).Result).Returns(IDP_GROUP_ID);
        _mockGithubApi.Setup(x => x.CreateTeam(GITHUB_ORG, TEAM_NAME).Result).Returns(("1", TEAM_SLUG));
        _mockGithubApi.Setup(x => x.GetTeams(GITHUB_ORG).Result).Returns(new List<(string, string, string)>());

        var args = new CreateTeamCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            TeamName = TEAM_NAME,
            IdpGroup = IDP_GROUP,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.CreateTeam(GITHUB_ORG, TEAM_NAME));
        _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[0]));
        _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[1]));
        _mockGithubApi.Verify(x => x.AddEmuGroupToTeam(GITHUB_ORG, TEAM_SLUG, IDP_GROUP_ID));
    }

    [Fact]
    public async Task Idempotency_Team_Exists()
    {
        _mockGithubApi.Setup(x => x.GetTeamMembers(GITHUB_ORG, TEAM_SLUG).Result).Returns(TEAM_MEMBERS);
        _mockGithubApi.Setup(x => x.GetIdpGroupId(GITHUB_ORG, IDP_GROUP).Result).Returns(IDP_GROUP_ID);
        _mockGithubApi.Setup(x => x.GetTeams(GITHUB_ORG).Result).Returns(new List<(string, string, string)> { ("1", TEAM_NAME, TEAM_SLUG) });

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var args = new CreateTeamCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            TeamName = TEAM_NAME,
            IdpGroup = IDP_GROUP,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.CreateTeam(GITHUB_ORG, TEAM_NAME), Times.Never);
        _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[0]));
        _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[1]));
        _mockGithubApi.Verify(x => x.AddEmuGroupToTeam(GITHUB_ORG, TEAM_SLUG, IDP_GROUP_ID));
        actualLogOutput.Should().Contain($"Team '{TEAM_NAME}' already exists. New team will not be created");
    }
}
