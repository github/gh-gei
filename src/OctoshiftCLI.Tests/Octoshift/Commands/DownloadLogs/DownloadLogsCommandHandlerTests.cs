using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Commands.DownloadLogs;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Commands.DownloadLogs;

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
        const string migrationId = "RM123";
        const string defaultFileName = $"migration-log-{githubOrg}-{repo}-{migrationId}.log";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
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
        const string migrationId = "RM123";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
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
        const string migrationId = "RM123";
        const string migrationLogFile = "migration-log-file";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
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
        const string logUrlPopulated = "some-url";
        const string migrationId = "RM123";
        const string defaultFileName = $"migration-log-{githubOrg}-{repo}-{migrationId}.log";

        _mockGithubApi.SetupSequence(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(("", migrationId))
            .ReturnsAsync(("", migrationId))
            .ReturnsAsync(("", migrationId))
            .ReturnsAsync(("", migrationId))
            .ReturnsAsync(("", migrationId))
            .ReturnsAsync((logUrlPopulated, migrationId));

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
    public async Task Calls_Download_When_File_Exists_At_Default_Path_And_Overwrite_Requested()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string migrationId = "RM123";
        const bool overwrite = true;

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        _handler.FileExists = filePath =>
        {
            return filePath == "migration-log-FooOrg-foo-repo-RM123.log";
        };

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
    public async Task Calls_Download_When_File_Exists_At_Custom_Path_And_Overwrite_Requested()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string migrationId = "RM123";
        const bool overwrite = true;

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        _handler.FileExists = filePath =>
        {
            return filePath == "happy_log_file.log";
        };

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
            Overwrite = overwrite,
            MigrationLogFile = "happy_log_file.log",
        };
        await _handler.Handle(args);

        // Assert
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));
    }

    [Fact]
    public async Task File_Already_Exists_At_Default_Path_No_Overwrite_Flag_Should_Throw_OctoshiftCliException()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string migrationId = "RM123";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        _handler.FileExists = filePath =>
        {
            return filePath == "migration-log-FooOrg-foo-repo-RM123.log";
        };

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
    public async Task File_Already_Exists_At_Custom_Path_No_Overwrite_Flag_Should_Throw_OctoshiftCliException()
    {
        // Arrange
        const string githubOrg = "FooOrg";
        const string repo = "foo-repo";
        const string logUrl = "some-url";
        const string migrationId = "RM123";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((logUrl, migrationId));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        _handler.FileExists = filePath =>
        {
            return filePath == "my_log_file.log";
        };

        // Assert
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = repo,
            MigrationLogFile = "my_log_file.log",
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
        const string migrationId = "RM123";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(("", migrationId));
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
