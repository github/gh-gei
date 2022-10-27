using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands;

public class DownloadLogsCommandHandlerTests
{
    private readonly DownloadLogsCommandHandler _handler;
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<HttpDownloadService> _mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
    private readonly Mock<OctoLogger> _mockLogger = TestHelpers.CreateMock<OctoLogger>();

    public DownloadLogsCommandHandlerTests()
    {
        _handler = new DownloadLogsCommandHandler(
            _mockLogger.Object,
            _mockGithubApi.Object,
            _mockHttpDownloadService.Object,
            new RetryPolicy(_mockLogger.Object) { _retryInterval = 0 });
    }

    [Fact]
    public async Task Happy_Path()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string defaultFileName = $"migration-log-{githubOrg}-{repo}.log";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await _handler.Handle(args);

        // Assert
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(logUrl, defaultFileName));
    }

    [Fact]
    public async Task Calls_GetMigrationLogUrl_With_Expected_Org_And_Repo()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, repo));
    }

    [Fact]
    public async Task Calls_Download_With_Expected_Migration_Log_File()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string migrationLogFile = "migration-log-file";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
            MigrationLogFile = migrationLogFile,
        };
        await _handler.Handle(args);

        // Assert
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(It.IsAny<string>(), migrationLogFile));
    }

    [Fact]
    public async Task Waits_For_Url_To_Populate()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrlEmpty = "";
        const string logUrlPopulated = "some-url";
        const string defaultFileName = $"migration-log-{githubOrg}-{repo}.log";

        _mockGithubApi.SetupSequence(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(logUrlEmpty)
            .ReturnsAsync(logUrlEmpty)
            .ReturnsAsync(logUrlEmpty)
            .ReturnsAsync(logUrlEmpty)
            .ReturnsAsync(logUrlEmpty)
            .ReturnsAsync(logUrlPopulated);

        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, repo), Times.Exactly(6));
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(logUrlPopulated, defaultFileName));
    }

    [Fact]
    public async Task Calls_Download_When_File_Exists_And_Overwrite_Requested()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const bool overwrite = true;

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
            Overwrite = overwrite,
        };
        await _handler.Handle(args);

        // Assert
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));
    }

    [Fact]
    public async Task File_Already_Exists_No_Overwrite_Flag_Should_Throw_OctoshiftCliException()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";

        // Act
        _handler.FileExists = _ => true;

        // Assert
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task Throw_OctoshiftCliException_When_No_Migration()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = null;

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

        // Assert
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task Throws_OctoshiftCliException_When_Migration_Log_Url_Doesnt_Populate_After_6_Attempts()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
        };
        await FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>();

        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, repo), Times.Exactly(6));
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    }
}
