using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Commands.WaitForMigration;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.WaitForMigration;

public class WaitForMigrationCommandBaseTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<WarningsCountLogger> _mockWarningsCountLogger = TestHelpers.CreateMock<WarningsCountLogger>();
    private readonly ServiceProvider _serviceProvider;
    private readonly WaitForMigrationCommandBase _command = [];

    private const string REPO_MIGRATION_ID = "RM_MIGRATION_ID";
    public WaitForMigrationCommandBaseTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddSingleton(_mockOctoLogger.Object)
            .AddSingleton(_mockGithubApiFactory.Object)
            .AddSingleton(_mockWarningsCountLogger.Object);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void It_Uses_The_TargetApiUrl_When_Provided()
    {
        var targetApiUrl = "TargetApiUrl";

        var args = new WaitForMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
            TargetApiUrl = targetApiUrl
        };

        _command.BuildHandler(args, _serviceProvider);

        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null, It.IsAny<string>()));
    }
}

