using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands.AbortMigration;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.AbortMigration;

public class AbortMigrationCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly AbortMigrationCommandHandler _handler;

    private const string REPO_MIGRATION_ID = "RM_MIGRATION_ID";

    public AbortMigrationCommandHandlerTests()
    {
        _handler = new AbortMigrationCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task With_Migration_ID_That_Succeeds()
    {
        // Arrange
        _mockGithubApi.Setup(x => x.AbortMigration(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var args = new AbortMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.AbortMigration(REPO_MIGRATION_ID), Times.Exactly(1));
        _mockGithubApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Migration_ID_That_Fails()
    {
        // Arrange
        _mockGithubApi.Setup(x => x.AbortMigration(It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var args = new AbortMigrationCommandArgs
        {
            MigrationId = REPO_MIGRATION_ID,
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.AbortMigration(REPO_MIGRATION_ID), Times.Exactly(1));
        _mockGithubApi.VerifyNoOtherCalls();
    }
}

