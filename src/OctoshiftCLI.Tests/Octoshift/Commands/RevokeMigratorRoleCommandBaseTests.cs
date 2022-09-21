using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class RevokeMigratorRoleCommandBaseTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly RevokeMigratorRoleCommandBaseHandler _command;

    private const string GITHUB_ORG = "FooOrg";
    private const string ACTOR = "foo-actor";
    private const string ACTOR_TYPE = "TEAM";

    public RevokeMigratorRoleCommandBaseTests()
    {
        _command = new RevokeMigratorRoleCommandBaseHandler(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var githubOrgId = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(githubOrgId);
        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        var args = new RevokeMigratorRoleArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
        };
        await _command.Handle(args);

        _mockGithubApi.Verify(x => x.RevokeMigratorRole(githubOrgId, ACTOR, ACTOR_TYPE));
    }

    [Fact]
    public async Task It_Uses_The_Github_Pat_When_Provided()
    {
        const string githubPat = "github-pat";

        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

        var args = new RevokeMigratorRoleArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
            GithubPat = githubPat,
        };
        await _command.Handle(args);

        _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
    }

    [Fact]
    public async Task Invalid_Actor_Type()
    {
        var args = new RevokeMigratorRoleArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = "INVALID",
        };
        await _command.Handle(args);
    }
}
