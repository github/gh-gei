using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands.GrantMigratorRole;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.GrantMigratorRole;

public class GrantMigratorRoleCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly GrantMigratorRoleCommandHandler _handler;

    private const string GITHUB_ORG = "FooOrg";
    private const string ACTOR = "foo-actor";
    private const string ACTOR_TYPE = "TEAM";

    public GrantMigratorRoleCommandHandlerTests()
    {
        _handler = new GrantMigratorRoleCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var githubOrgId = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(githubOrgId);

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Actor = ACTOR,
            ActorType = ACTOR_TYPE,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.GrantMigratorRole(githubOrgId, ACTOR, ACTOR_TYPE));
    }
}
