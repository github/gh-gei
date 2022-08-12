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

    private readonly RevokeMigratorRoleCommandBase _command;

    public RevokeMigratorRoleCommandBaseTests()
    {
        _command = new RevokeMigratorRoleCommandBase(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var githubOrg = "FooOrg";
        var actor = "foo-actor";
        var actorType = "TEAM";
        var githubOrgId = Guid.NewGuid().ToString();

        _mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

        await _command.Handle(githubOrg, actor, actorType);

        _mockGithubApi.Verify(x => x.RevokeMigratorRole(githubOrgId, actor, actorType));
    }

    [Fact]
    public async Task It_Uses_The_Github_Pat_When_Provided()
    {
        const string githubPat = "github-pat";

        _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

        await _command.Handle("githubOrg", "actor", "TEAM", githubPat);

        _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
    }

    [Fact]
    public async Task Invalid_Actor_Type()
    {
        await _command.Handle("foo", "foo", "foo");
    }
}
