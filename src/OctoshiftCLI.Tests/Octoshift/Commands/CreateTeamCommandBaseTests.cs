using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Contracts;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class CreateTeamCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ServiceProvider _serviceProvider;
    private readonly CreateTeamCommandBase _command = new();

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

        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), args.GithubPat), Times.Once);
    }
}
