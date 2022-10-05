using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class MigrateRepoCommandTests
{
    private const string SSH_USER = "ssh-user";
    private const string SSH_PRIVATE_KEY = "ssh-private-key";
    private const int SSH_PORT = 1234;
    private const string BBS_SHARED_HOME = "shared-home";
    private const string BBS_HOST = "bbs-host";
    private const string BBS_SERVER_URL = $"https://{BBS_HOST}";
    private const string GITHUB_ORG = "github-org";
    private const string GITHUB_PAT = "github-pat";
    private const string BBS_USERNAME = "bbs-username";
    private const string BBS_PASSWORD = "bbs-password";
    private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
    private readonly Mock<BbsApiFactory> _mockBbsApiFactory = TestHelpers.CreateMock<BbsApiFactory>();
    private readonly Mock<BbsArchiveDownloaderFactory> _mockBbsArchiveDownloaderFactory = TestHelpers.CreateMock<BbsArchiveDownloaderFactory>();
    private readonly Mock<IAzureApiFactory> _mockAzureApiFactory = new();

    private readonly MigrateRepoCommand _command = new();

    public MigrateRepoCommandTests()
    {
        _mockServiceProvider.Setup(m => m.GetService(typeof(OctoLogger))).Returns(_mockOctoLogger.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(EnvironmentVariableProvider))).Returns(_mockEnvironmentVariableProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(FileSystemProvider))).Returns(_mockFileSystemProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(GithubApiFactory))).Returns(_mockGithubApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(BbsApiFactory))).Returns(_mockBbsApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(BbsArchiveDownloaderFactory))).Returns(_mockBbsArchiveDownloaderFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(IAzureApiFactory))).Returns(_mockAzureApiFactory.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("migrate-repo");
        _command.Options.Count.Should().Be(19);

        TestHelpers.VerifyCommandOption(_command.Options, "bbs-server-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-project", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-repo", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-username", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-password", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-shared-home", false);
        TestHelpers.VerifyCommandOption(_command.Options, "archive-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "archive-path", false);
        TestHelpers.VerifyCommandOption(_command.Options, "azure-storage-connection-string", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-org", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-repo", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-private-key", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(_command.Options, "smb-user", false, true);
        TestHelpers.VerifyCommandOption(_command.Options, "smb-password", false, true);
        TestHelpers.VerifyCommandOption(_command.Options, "wait", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Ssh_Archive_Downloader_When_Ssh_User_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            BbsSharedHome = BBS_SHARED_HOME,
            BbsServerUrl = BBS_SERVER_URL
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSshDownloader(BBS_HOST, SSH_USER, SSH_PRIVATE_KEY, SSH_PORT, BBS_SHARED_HOME));
    }

    [Fact]
    public void BuildHandler_Creates_The_Handler()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs();

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockBbsApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSshDownloader(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSmbDownloader(), Times.Never);
        _mockAzureApiFactory.Verify(m => m.Create(It.IsAny<string>()), Times.Never);
        _mockAzureApiFactory.Verify(m => m.CreateClientNoSsl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildHandler_Creates_GitHub_Api_When_Github_Org_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubPat = GITHUB_PAT
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockGithubApiFactory.Verify(m => m.Create(null, GITHUB_PAT));
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Api_When_Github_Org_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD));
    }

    [Fact]
    public void BuildHandler_Creates_Azure_Api_Factory_When_Azure_Storage_Connection_String_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockAzureApiFactory.Verify(m => m.Create(AZURE_STORAGE_CONNECTION_STRING));
    }
}
