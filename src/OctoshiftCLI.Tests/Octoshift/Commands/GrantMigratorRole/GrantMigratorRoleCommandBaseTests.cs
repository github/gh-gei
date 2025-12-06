using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands.GrantMigratorRole;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.GrantMigratorRole;

public class GrantMigratorRoleCommandBaseTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ServiceProvider _serviceProvider;
    private readonly GrantMigratorRoleCommandBase _command = [];

    public GrantMigratorRoleCommandBaseTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockGithubApiFactory.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void It_Uses_Github_Target_Pat_When_Provided()
    {
        var githubPat = Guid.NewGuid().ToString();

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = "foo",
            Actor = "blah",
            ActorType = "TEAM",
            GithubPat = githubPat
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), githubPat));
    }

    [Fact]
    public void It_Uses_The_GhesApiUrl_When_Provided()
    {
        var ghesApiUrl = "GHES-API-URL";

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = "foo",
            Actor = "blah",
            ActorType = "TEAM",
            GhesApiUrl = ghesApiUrl
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(ghesApiUrl, It.IsAny<string>(), null));
    }

    [Fact]
    public void It_Uses_The_TargetApiUrl_When_Provided()
    {
        var targetApiUrl = "TARGET-API-URL";

        var args = new GrantMigratorRoleCommandArgs
        {
            GithubOrg = "foo",
            Actor = "blah",
            ActorType = "TEAM",
            TargetApiUrl = targetApiUrl,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, It.IsAny<string>(), null));
    }
}
