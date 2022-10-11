using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class RevokeMigratorRoleCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly RevokeMigratorRoleCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string ACTOR = "foo-actor";
    private const string ACTOR_TYPE = "TEAM";

    public RevokeMigratorRoleCommandHandlerTests()
    {
        _handler = new RevokeMigratorRoleCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var githubOrgId = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(githubOrgId);

        var args = new RevokeMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.RevokeMigratorRole(githubOrgId, ACTOR, ACTOR_TYPE));
    }

    [Fact]
    public async Task Invalid_Actor_Type()
    {
        var args = new RevokeMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = "INVALID",
        };
        await _handler.Handle(args);

        _mockOctoLogger.Verify(x => x.LogError(It.IsAny<string>()));
    }

    [Fact]
    public async Task It_Uses_The_GhesApiUrl_When_Provided()
    {
        const string ghesApiUrl = "GhesApiUrl";

        _mockGithubApiFactory.Setup(m => m.Create(ghesApiUrl, It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var args = new RevokeMigratorRoleArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
            GhesApiUrl = ghesApiUrl,
        };
        await _handler.Handle(args);

        _mockGithubApiFactory.Verify(m => m.Create(ghesApiUrl, null));
    }

}
