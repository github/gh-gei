using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands.ReclaimMannequin;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandBaseTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ConfirmationService> _mockConfirmationService = TestHelpers.CreateMock<ConfirmationService>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ReclaimMannequinCommandBase _command = [];

    private const string GITHUB_ORG = "FooOrg";

    public ReclaimMannequinCommandBaseTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockGithubApiFactory.Object)
            .AddSingleton(_mockConfirmationService.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void It_Uses_The_TargetApiUrl_When_Provided()
    {
        var targetApiUrl = "TARGET-API-URL";

        var args = new ReclaimMannequinCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            Csv = "file.csv",
            TargetApiUrl = targetApiUrl,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null, null));
    }
}
