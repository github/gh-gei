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

    [Fact]
    public async Task Should_Throw_When_Neither_MigrationId_Nor_OrgRepo_Provided()
    {
        // Act & Assert
        var args = new DownloadLogsCommandArgs();
        await FluentAssertions.FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>()
            .WithMessage("Either --migration-id (GraphQL migration ID) or both --github-org and --github-repo must be specified.");
    }

    [Fact]
    public async Task Should_Throw_When_Only_GithubOrg_Provided()
    {
        // Act & Assert
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = "test-org"
        };
        await FluentAssertions.FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>()
            .WithMessage("Either --migration-id (GraphQL migration ID) or both --github-org and --github-repo must be specified.");
    }

    [Fact]
    public async Task Should_Throw_When_Only_GithubRepo_Provided()
    {
        // Act & Assert
        var args = new DownloadLogsCommandArgs
        {
            GithubRepo = "test-repo"
        };
        await FluentAssertions.FluentActions
            .Invoking(async () => await _handler.Handle(args))
            .Should().ThrowAsync<OctoshiftCliException>()
            .WithMessage("Either --migration-id (GraphQL migration ID) or both --github-org and --github-repo must be specified.");
    }

    [Fact]
    public async Task Should_Log_Warning_When_MigrationId_And_OrgRepo_Both_Provided()
    {
        // Arrange
        const string migrationId = "RM_test123";
        const string githubOrg = "test-org";
        const string githubRepo = "test-repo";
        const string logUrl = "some-url";
        const string repoName = "test-repo-name";

        _mockGithubApi.Setup(m => m.GetMigration(migrationId))
            .ReturnsAsync((State: "SUCCEEDED", RepositoryName: repoName, WarningsCount: 0, FailureReason: "", MigrationLogUrl: logUrl));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            MigrationId = migrationId,
            GithubOrg = githubOrg,
            GithubRepo = githubRepo
        };
        await _handler.Handle(args);

        // Assert
        _mockLogger.Verify(m => m.LogWarning("--github-org and --github-repo are ignored when --migration-id is specified."), Times.Once);
        _mockGithubApi.Verify(m => m.GetMigration(migrationId), Times.Once);
        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Should_Succeed_When_Only_MigrationId_Provided()
    {
        // Arrange
        const string migrationId = "RM_test123";
        const string logUrl = "some-url";
        const string repoName = "test-repo-name";
        const string expectedFileName = $"migration-log-{repoName}-{migrationId}.log";

        _mockGithubApi.Setup(m => m.GetMigration(migrationId))
            .ReturnsAsync((State: "SUCCEEDED", RepositoryName: repoName, WarningsCount: 0, FailureReason: "", MigrationLogUrl: logUrl));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            MigrationId = migrationId
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.GetMigration(migrationId), Times.Once);
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(logUrl, expectedFileName), Times.Once);
        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Should_Succeed_When_Only_OrgRepo_Provided()
    {
        // Arrange
        const string githubOrg = "test-org";
        const string githubRepo = "test-repo";
        const string logUrl = "some-url";
        const string migrationId = "RM_test123";
        const string expectedFileName = $"migration-log-{githubOrg}-{githubRepo}-{migrationId}.log";

        _mockGithubApi.Setup(m => m.GetMigrationLogUrl(githubOrg, githubRepo))
            .ReturnsAsync((logUrl, migrationId));
        _mockHttpDownloadService.Setup(m => m.DownloadToFile(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var args = new DownloadLogsCommandArgs
        {
            GithubOrg = githubOrg,
            GithubRepo = githubRepo
        };
        await _handler.Handle(args);

        // Assert
        _mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, githubRepo), Times.Once);
        _mockHttpDownloadService.Verify(m => m.DownloadToFile(logUrl, expectedFileName), Times.Once);
        _mockGithubApi.Verify(m => m.GetMigration(It.IsAny<string>()), Times.Never);
    }
}
