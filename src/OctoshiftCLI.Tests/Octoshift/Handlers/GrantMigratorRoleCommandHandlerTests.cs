using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class GrantMigratorRoleCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly GrantMigratorRoleCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string ACTOR = "foo-actor";
    private const string ACTOR_TYPE = "TEAM";

    public GrantMigratorRoleCommandHandlerTests()
    {
        _handler = new GrantMigratorRoleCommandHandler(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var githubOrgId = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(githubOrgId);
        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.GrantMigratorRole(githubOrgId, ACTOR, ACTOR_TYPE));
    }

    [Fact]
    public async Task It_Uses_The_Github_Pat_When_Provided()
    {
        const string githubPat = "github-pat";

        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
            GithubPat = githubPat,
        };
        await _handler.Handle(args);

        _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
    }

    [Fact]
    public async Task Invalid_Actor_Type()
    {
        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = "INVALID",
        };
        await _handler.Handle(args);
    }

    [Fact]
    public async Task It_Uses_The_GhesApiUrl_When_Provided()
    {
        const string ghesApiUrl = "GHES-API-URL";

        _mockGithubApiFactory.Setup(x => x.Create(ghesApiUrl, It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
            GhesApiUrl = ghesApiUrl
        };
        await _handler.Handle(args);

        _mockGithubApiFactory.Verify(m => m.Create(ghesApiUrl, null));
    }
}
