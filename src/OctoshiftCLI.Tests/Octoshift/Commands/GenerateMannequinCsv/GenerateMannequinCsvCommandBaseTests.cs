using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands.GenerateMannequinCsv;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.GeneerateMannequinCsv;

public class GenerateMannequinCsvCommandBaseTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly GenerateMannequinCsvCommandBase _command = [];
    private const string GITHUB_ORG = "FooOrg";

    public GenerateMannequinCsvCommandBaseTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockGithubApiFactory.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void It_Uses_The_TargetApiUrl_When_Provided()
    {
        var targetApiUrl = "TARGET-API-URL";

        var args = new GenerateMannequinCsvCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            IncludeReclaimed = false,
            TargetApiUrl = targetApiUrl,
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null, null));
    }
}
