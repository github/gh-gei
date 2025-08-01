using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands.CreateTeam;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.CreateTeam;

public class CreateTeamCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ServiceProvider _serviceProvider;
    private readonly CreateTeamCommandBase _command = [];

    public CreateTeamCommandTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockGithubApiFactory.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void It_Uses_Github_Source_Pat_When_Provided()
    {
        var githubPat = Guid.NewGuid().ToString();

        var args = new CreateTeamCommandArgs
        {
            GithubOrg = "foo",
            TeamName = "blah",
            GithubPat = githubPat
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), args.GithubPat), Times.Once);
    }

    [Fact]
    public void It_Uses_Target_Api_Url_When_Provided()
    {
        var targetApiUrl = "https://api.github.com";

        var args = new CreateTeamCommandArgs
        {
            GithubOrg = "foo",
            TeamName = "blah",
            TargetApiUrl = targetApiUrl
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(args.TargetApiUrl, It.IsAny<string>(), null), Times.Once);
    }
}
