using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandTests
{
    private const string ARCHIVE_DOWNLOAD_HOST = "archive-download-host";
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
    private const string SMB_USER = "smb-user";
    private const string SMB_PASSWORD = "smb-password";
    private const string SMB_DOMAIN = "smb-domain";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<BbsApiFactory> _mockBbsApiFactory = TestHelpers.CreateMock<BbsApiFactory>();
    private readonly Mock<BbsArchiveDownloaderFactory> _mockBbsArchiveDownloaderFactory = TestHelpers.CreateMock<BbsArchiveDownloaderFactory>();
    private readonly Mock<IAzureApiFactory> _mockAzureApiFactory = new();
    private readonly Mock<WarningsCountLogger> _warningsCountLogger = TestHelpers.CreateMock<WarningsCountLogger>();

    private readonly MigrateRepoCommand _command = [];

    public MigrateRepoCommandTests()
    {
        _mockServiceProvider.Setup(m => m.GetService(typeof(OctoLogger))).Returns(_mockOctoLogger.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(EnvironmentVariableProvider))).Returns(_mockEnvironmentVariableProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(FileSystemProvider))).Returns(_mockFileSystemProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(ITargetGithubApiFactory))).Returns(_mockGithubApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(BbsApiFactory))).Returns(_mockBbsApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(BbsArchiveDownloaderFactory))).Returns(_mockBbsArchiveDownloaderFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(IAzureApiFactory))).Returns(_mockAzureApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(WarningsCountLogger))).Returns(_warningsCountLogger.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        var command = new MigrateRepoCommand();
        command.Should().NotBeNull();
        command.Name.Should().Be("migrate-repo");
        command.Options.Count.Should().Be(33);

        TestHelpers.VerifyCommandOption(command.Options, "bbs-server-url", true);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-project", true);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-repo", true);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-username", false);
        TestHelpers.VerifyCommandOption(command.Options, "bbs-password", false);
        TestHelpers.VerifyCommandOption(command.Options, "archive-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "archive-path", false);
        TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
        TestHelpers.VerifyCommandOption(command.Options, "aws-bucket-name", false);
        TestHelpers.VerifyCommandOption(command.Options, "aws-access-key", false);
        TestHelpers.VerifyCommandOption(command.Options, "aws-session-token", false);
        TestHelpers.VerifyCommandOption(command.Options, "aws-region", false);
        TestHelpers.VerifyCommandOption(command.Options, "aws-secret-key", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-org", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-repo", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "archive-download-host", false);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-user", false);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-private-key", false);
        TestHelpers.VerifyCommandOption(command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(command.Options, "smb-user", false);
        TestHelpers.VerifyCommandOption(command.Options, "smb-password", false);
        TestHelpers.VerifyCommandOption(command.Options, "smb-domain", false);
        TestHelpers.VerifyCommandOption(command.Options, "queue-only", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-repo-visibility", false);
        TestHelpers.VerifyCommandOption(command.Options, "kerberos", false, true);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "keep-archive", false);
        TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-uploads-url", false, true);
        TestHelpers.VerifyCommandOption(command.Options, "use-github-storage", false, true);
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Ssh_Archive_Downloader_Based_On_Server_Url_When_Ssh_User_Is_Provided()
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
    public void BuildHandler_Creates_Bbs_Ssh_Archive_Downloader_When_Ssh_User_And_Archive_Download_Host_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
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
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSshDownloader(ARCHIVE_DOWNLOAD_HOST, SSH_USER, SSH_PRIVATE_KEY, SSH_PORT, BBS_SHARED_HOME));
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Smb_Archive_Downloader_Based_On_Server_Url_When_Smb_User_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            SmbUser = SMB_USER,
            SmbPassword = SMB_PASSWORD,
            SmbDomain = SMB_DOMAIN,
            BbsSharedHome = BBS_SHARED_HOME,
            BbsServerUrl = BBS_SERVER_URL
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSmbDownloader(BBS_HOST, SMB_USER, SMB_PASSWORD, SMB_DOMAIN, BBS_SHARED_HOME));
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Smb_Archive_Downloader_When_Smb_User_And_Archive_Download_Host_Is_Provided()
    {
        // Arrange
        var args = new MigrateRepoCommandArgs
        {
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            SmbUser = SMB_USER,
            SmbPassword = SMB_PASSWORD,
            SmbDomain = SMB_DOMAIN,
            BbsSharedHome = BBS_SHARED_HOME,
            BbsServerUrl = BBS_SERVER_URL
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSmbDownloader(ARCHIVE_DOWNLOAD_HOST, SMB_USER, SMB_PASSWORD, SMB_DOMAIN, BBS_SHARED_HOME));
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

        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), null, It.IsAny<string>()), Times.Never);
        _mockBbsApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSshDownloader(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _mockBbsArchiveDownloaderFactory.Verify(m => m.CreateSmbDownloader(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

        _mockGithubApiFactory.Verify(m => m.Create(null, null, GITHUB_PAT));
    }

    [Fact]
    public void BuildHandler_Uses_Target_Api_Url_When_Provided()
    {
        // Arrange
        var targetApiUrl = "https://api.github.com";
        var args = new MigrateRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubPat = GITHUB_PAT,
            TargetApiUrl = targetApiUrl
        };

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null, GITHUB_PAT));
    }

    [Fact]
    public void BuildHandler_Creates_Bbs_Api_When_Bbs_Server_Url_Is_Provided()
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

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, false));
    }

    [Fact]
    public void BuildHandler_Creates_Azure_Api_Factory_When_Azure_Storage_Connection_String_Is_Provided_Via_Args()
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

    [Fact]
    public void BuildHandler_Creates_Azure_Api_Factory_When_Azure_Storage_Connection_String_Is_Provided_Via_Environment_Variables()
    {
        // Arrange
        _mockEnvironmentVariableProvider.Setup(m => m.AzureStorageConnectionString(false)).Returns(AZURE_STORAGE_CONNECTION_STRING);

        var args = new MigrateRepoCommandArgs();

        // Act
        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        // Assert
        handler.Should().NotBeNull();

        _mockAzureApiFactory.Verify(m => m.Create(AZURE_STORAGE_CONNECTION_STRING));
    }

    [Fact]
    public void It_Gets_A_Kerberos_HttpClient_When_Kerberos_Is_True()
    {
        var args = new MigrateRepoCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            Kerberos = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.CreateKerberos(BBS_SERVER_URL, false));
    }

    [Fact]
    public void It_Gets_A_Kerberos_With_No_Ssl_Verify_HttpClient_When_Kerberos_And_No_Ssl_Verify_Are_True()
    {
        var args = new MigrateRepoCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            Kerberos = true,
            NoSslVerify = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.CreateKerberos(BBS_SERVER_URL, true));
    }

    [Fact]
    public void It_Gets_A_Default_HttpClient_When_Kerberos_And_No_Ssl_Verify_Are_Not_Set()
    {
        var args = new MigrateRepoCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, false));
    }

    [Fact]
    public void It_Gets_A_No_Ssl_Verify_HttpClient_When_No_Ssl_Verify_Is_True()
    {
        var args = new MigrateRepoCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            NoSslVerify = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, BBS_USERNAME, BBS_PASSWORD, true));
    }
}
