using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.BbsToGithub.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Handlers
{
    public class MigrateRepoCommandHandlerTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly Mock<AzureApi> _mockAzureApi = TestHelpers.CreateMock<AzureApi>();
        private readonly Mock<AwsApi> _mockAwsApi = TestHelpers.CreateMock<AwsApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<IBbsArchiveDownloader> _mockBbsArchiveDownloader = new();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

        private readonly MigrateRepoCommandHandler _handler;

        private const string ARCHIVE_PATH = "path/to/archive.tar";
        private const string ARCHIVE_URL = "https://archive-url/bbs-archive.tar";
        private readonly byte[] ARCHIVE_DATA = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        private const string GITHUB_ORG = "target-org";
        private const string GITHUB_REPO = "target-repo";
        private const string GITHUB_PAT = "github pat";
        private const string AWS_BUCKET_NAME = "aws-bucket-name";
        private const string AWS_ACCESS_KEY_ID = "aws-access-key-id";
        private const string AWS_SECRET_ACCESS_KEY = "aws-secret-access-key";
        private const string AWS_ACCESS_KEY = "aws-access-key";
        private const string AWS_SECRET_KEY = "aws-secret-key";
        private const string AWS_SESSION_TOKEN = "aws-session-token";
        private const string AWS_REGION = "aws-region";
        private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";

        private const string BBS_HOST = "our-bbs-server.com";
        private const string BBS_SERVER_URL = $"https://{BBS_HOST}";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const string BBS_PROJECT = "bbs-project";
        private const string BBS_REPO = "bbs-repo";
        private const string SSH_USER = "ssh-user";
        private const string PRIVATE_KEY = "private-key";
        private const string SMB_USER = "smb-user";
        private const string SMB_PASSWORD = "smb-password";
        private const long BBS_EXPORT_ID = 123;

        private const string GITHUB_ORG_ID = "github-org-id";
        private const string MIGRATION_SOURCE_ID = "migration-source-id";
        private const string MIGRATION_ID = "migration-id";

        public MigrateRepoCommandHandlerTests()
        {
            _handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockBbsApi.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockBbsArchiveDownloader.Object,
                _mockAzureApi.Object,
                _mockAwsApi.Object,
                _mockFileSystemProvider.Object
            );
        }

        [Fact]
        public async Task Happy_Path_Generate_Only()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
            };
            await _handler.Handle(args);

            // Assert
            _mockBbsApi.Verify(m => m.StartExport(
                BBS_PROJECT,
                BBS_REPO
            ));
        }

        [Fact]
        public async Task Happy_Path_Generate_And_Download()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
            };
            await _handler.Handle(args);

            // Assert
            _mockBbsArchiveDownloader.Verify(m => m.Download(BBS_EXPORT_ID, It.IsAny<string>()));
        }

        [Fact]
        public async Task Happy_Path_Ingest_Only()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Happy_Path_Generate_Archive_Ssh_Download_Azure_Upload_And_Ingest()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ReturnsAsync(new Uri(ARCHIVE_URL));
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Happy_Path_Generate_Archive_Ssh_Download_Aws_Upload_And_Ingest()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Happy_Path_Full_Flow_Running_On_Bbs_Server()
        {
            // Arrange
            const string bbsSharedHome = "bbs-shared-home";
            var archivePath = $"{bbsSharedHome}/data/migration/export/Bitbucket_export_{BBS_EXPORT_ID}.tar";

            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(archivePath)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ReturnsAsync(new Uri(ARCHIVE_URL));
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
                BbsSharedHome = bbsSharedHome
            };

            var handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockBbsApi.Object,
                _mockEnvironmentVariableProvider.Object,
                null, // in case of running on Bitbucket server, the downloader will be null
                _mockAzureApi.Object,
                _mockAwsApi.Object,
                _mockFileSystemProvider.Object
            );
            await handler.Handle(args);

            // Assert
            args.ArchivePath.Should().Be(archivePath);

            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Happy_Path_Full_Flow_Bbs_Credentials_Via_Environment()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.BbsUsername(It.IsAny<bool>())).Returns(BBS_USERNAME);
            _mockEnvironmentVariableProvider.Setup(m => m.BbsPassword(It.IsAny<bool>())).Returns(BBS_PASSWORD);
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ReturnsAsync(new Uri(ARCHIVE_URL));
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Happy_Path_Deletes_Downloaded_Archive()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ReturnsAsync(new Uri(ARCHIVE_URL));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT
            };
            await _handler.Handle(args);

            // Assert
            _mockFileSystemProvider.Verify(m => m.DeleteIfExists(ARCHIVE_PATH));
        }

        [Fact]
        public async Task It_Deletes_Downloaded_Archive_Even_If_Upload_Fails()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ThrowsAsync(new InvalidOperationException());

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT
            };
            await _handler.Invoking(async x => await x.Handle(args)).Should().ThrowExactlyAsync<InvalidOperationException>();

            // Assert
            _mockFileSystemProvider.Verify(m => m.DeleteIfExists(ARCHIVE_PATH));
        }

        [Fact]
        public async Task Happy_Path_Does_Not_Throw_If_Fails_To_Delete_Downloaded_Archive()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), ARCHIVE_DATA)).ReturnsAsync(new Uri(ARCHIVE_URL));
            _mockFileSystemProvider.Setup(x => x.DeleteIfExists(It.IsAny<string>())).Throws(new UnauthorizedAccessException("Access Denied"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT
            };
            await _handler.Handle(args);

            // Assert
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(ARCHIVE_PATH));
        }

        [Fact]
        public async Task Uses_GitHub_Pat_When_Provided_As_Option()
        {
            // Arrange
            var githubPat = "specific github pat";

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = githubPat
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                githubPat,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _handler.Handle(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
        }

        [Fact]
        public async Task Throws_An_Error_If_Export_Fails()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("FAILED", "The export failed", 0));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                Wait = true
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_User_And_Smb_User_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SmbUser = SMB_USER
            };
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_User_And_Smb_Password_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SmbPassword = SMB_PASSWORD
            };
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_Private_Key_And_Smb_User_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshPrivateKey = PRIVATE_KEY,
                SmbUser = SMB_USER
            };
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Both_Ssh_Private_Key_And_Smb_Password_Are_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                SmbPassword = SMB_PASSWORD
            };
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Ssh_User_Is_Provided_And_Ssh_Private_Key_Is_Not_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER
            };

            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Uses_Archive_Path_If_Provided()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            var archiveBytes = Encoding.ASCII.GetBytes("here are some bytes");
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(archiveBytes);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), archiveBytes)).ReturnsAsync(new Uri(ARCHIVE_URL));

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Errors_If_Archive_Url_And_Archive_Path_Are_Passed()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--archive-path*--archive-url*");
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Throws_When_Smb_User_Is_Provided_And_Smb_Password_Is_Not_Provided()
        {
            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SmbUser = SMB_USER
            };

            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Invoke_With_Bbs_Server_Url_Should_Not_Throw_When_Smb_User_Is_Provided_And_Smb_Password_Is_Provided_Via_Environment_Variable()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));

            _mockEnvironmentVariableProvider.Setup(m => m.SmbPassword(It.IsAny<bool>())).Returns(SMB_PASSWORD);

            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
            };

            // Act, Assert
            await _handler.Invoking(x => x.Handle(args)).Should().NotThrowAsync();
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_And_Archive_Url_Are_Passed()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-server-url*--archive-url*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_And_Archive_Path_Are_Passed()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-server-url*--archive-path*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Archive_Path_And_Archive_Url_Are_Not_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-server-url*--archive-path*--archive-url*");
        }

        [Fact]
        public async Task It_Does_Not_Set_The_Archive_Path_When_Archive_Path_Is_Provided()
        {
            // Arrange
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>())).ReturnsAsync(new Uri(ARCHIVE_URL));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _handler.Handle(args);

            // Assert
            args.ArchivePath.Should().Be(ARCHIVE_PATH);
            _mockFileSystemProvider.Verify(m => m.ReadAllBytesAsync(ARCHIVE_PATH));
        }

        [Fact]
        public async Task It_Does_Not_Set_The_Archive_Path_When_Archive_Url_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _handler.Handle(args);

            // Assert
            args.ArchivePath.Should().BeNull();
            _mockFileSystemProvider.Verify(m => m.ReadAllBytesAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Uses_Aws_If_Credentials_Are_Passed()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ArchivePath = ARCHIVE_PATH,
                AwsAccessKey = AWS_ACCESS_KEY_ID,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY,
                AwsBucketName = AWS_BUCKET_NAME
            };

            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));

            _mockAwsApi.Verify(m => m.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>()));
        }

        [Fact]
        public async Task It_Throws_When_Both_Azure_Storage_Connection_String_And_Aws_Bucket_Name_Are_Not_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(
                    new MigrateRepoCommandArgs
                    {
                        ArchivePath = ARCHIVE_PATH,
                        GithubOrg = GITHUB_ORG,
                        GithubRepo = GITHUB_REPO
                    }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--azure-storage-connection-string*AZURE_STORAGE_CONNECTION_STRING*or*--aws-bucket-name*--aws-access-key*AWS_ACCESS_KEY_ID*--aws-secret-key*AWS_SECRET_ACCESS_KEY*");
        }

        [Fact]
        public async Task It_Throws_When_Both_Azure_Storage_Connection_String_And_Aws_Bucket_Name_Are_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(
                    new MigrateRepoCommandArgs
                    {
                        ArchivePath = ARCHIVE_PATH,
                        GithubOrg = GITHUB_ORG,
                        GithubRepo = GITHUB_REPO,
                        AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                        AwsBucketName = AWS_BUCKET_NAME
                    }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--azure-storage-connection-string*AZURE_STORAGE_CONNECTION_STRING*and*--aws-bucket-name*--aws-access-key*AWS_ACCESS_KEY_ID*--aws-secret-key*AWS_SECRET_ACCESS_KEY*");
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Is_Provided_But_But_No_Aws_Access_Key_Id()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AwsBucketName = AWS_BUCKET_NAME
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-access-key*AWS_ACCESS_KEY_ID*");
        }

        [Fact]
        public async Task It_Does_Not_Throw_When_Aws_Bucket_Name_Is_Provided_And_Can_Fallback_To_Aws_Access_Key_Environment_Variable()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
#pragma warning disable CS0618
            _mockEnvironmentVariableProvider.Setup(x => x.AwsAccessKey(false)).Returns(AWS_ACCESS_KEY);
#pragma warning restore CS0618

            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
            };
            await _handler.Invoking(async x => await x.Handle(args)).Should().NotThrowAsync();

            _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(msg => msg.Contains("AWS_ACCESS_KEY"))));
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Secret_Access_Key()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-secret-key*AWS_SECRET_ACCESS_KEY*");
        }

        [Fact]
        public async Task It_Does_Not_Throw_When_Aws_Bucket_Name_Is_Provided_And_Can_Fallback_To_Aws_Secret_Key_Environment_Variable()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
#pragma warning disable CS0618
            _mockEnvironmentVariableProvider.Setup(x => x.AwsSecretKey(false)).Returns(AWS_SECRET_KEY);
#pragma warning restore CS0618

            // Act, Assert
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
            };
            await _handler.Invoking(async x => await x.Handle(args)).Should().NotThrowAsync();

            _mockOctoLogger.Verify(m => m.LogWarning(It.Is<string>(msg => msg.Contains("AWS_SECRET_KEY"))));
        }

        [Fact]
        public async Task It_Throws_When_Aws_Session_Token_Is_Provided_But_Aws_Region_Is_Not()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY,
                AwsSessionToken = AWS_SESSION_TOKEN
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-region*AWS_REGION*--aws-session-token*AWS_SESSION_TOKEN*");
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Access_Key_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Secret_Key_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Session_Token_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsSessionToken = AWS_SESSION_TOKEN
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task It_Throws_When_Aws_Bucket_Name_Not_Provided_But_Aws_Region_Provided()
        {
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                AwsRegion = AWS_REGION
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Provided_But_No_Bbs_Username()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*BBS_USERNAME*--bbs-username*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Provided_But_No_Bbs_Password()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsUsername = BBS_USERNAME
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*BBS_PASSWORD*--bbs-password*");
        }

        [Fact]
        public async Task It_Should_Not_Validate_Bbs_Username_And_Password_When_Kerberos_Is_Set()
        {
            // Arrange
            _mockBbsApi.Setup(x => x.GetExport(It.IsAny<long>())).ReturnsAsync(("COMPLETED", "The export is complete", 100));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                Kerberos = true
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .NotThrowAsync();
        }

        [Fact]
        public async Task It_Throws_When_Kerberos_Is_Set_And_Bbs_Username_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                Kerberos = true
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--kerberos*");
        }

        [Fact]
        public async Task It_Throws_When_Kerberos_Is_Set_And_Bbs_Password_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsPassword = BBS_PASSWORD,
                Kerberos = true
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-password*--kerberos*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Not_Provided_But_Bbs_Username_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsUsername = BBS_USERNAME
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--bbs-password*--bbs-server-url*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Not_Provided_But_Bbs_Password_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                BbsPassword = BBS_USERNAME
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--bbs-username*--bbs-password*--bbs-server-url*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Not_Provided_But_Ssh_User_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--bbs-server-url*");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Not_Provided_But_Smb_User_Is_Provided()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                SmbUser = SMB_USER,
                SmbPassword = SMB_PASSWORD,
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*SSH*SMB*--bbs-server-url*");
        }

        [Fact]
        public async Task It_Throws_If_Github_Org_Is_Provided_But_Github_Repo_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                BbsServerUrl = BBS_SERVER_URL,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                GithubPat = GITHUB_PAT,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--github-repo*");
        }

        [Fact]
        public async Task It_Throws_If_Archive_Url_Is_Provided_But_Github_Org_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubPat = GITHUB_PAT,
                ArchiveUrl = ARCHIVE_URL,
                GithubRepo = GITHUB_REPO
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--github-org*");
        }

        [Fact]
        public async Task It_Throws_If_Archive_Url_Is_Provided_But_Github_Repo_Is_Not()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubPat = GITHUB_PAT,
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("*--github-repo*");
        }
    }
}
