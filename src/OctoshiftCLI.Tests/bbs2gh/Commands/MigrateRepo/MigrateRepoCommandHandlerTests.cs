using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.BbsToGithub.Commands.MigrateRepo;
using OctoshiftCLI.BbsToGithub.Services;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.MigrateRepo
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

        private readonly WarningsCountLogger _warningsCountLogger;
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
        private const string AWS_SESSION_TOKEN = "aws-session-token";
        private const string AWS_REGION = "eu-west-1";
        private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";

        private const string BBS_HOST = "our-bbs-server.com";
        private const string BBS_SERVER_URL = $"https://{BBS_HOST}";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const string BBS_PROJECT = "bbs-project";
        private const string BBS_REPO = "bbs-repo";
        private const string BBS_REPO_URL = $"{BBS_SERVER_URL}/projects/{BBS_PROJECT}/repos/{BBS_REPO}/browse";
        private const string UNUSED_REPO_URL = "https://not-used";
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
            _warningsCountLogger = new WarningsCountLogger(_mockOctoLogger.Object);
            _handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockBbsApi.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockBbsArchiveDownloader.Object,
                _mockAzureApi.Object,
                _mockAwsApi.Object,
                _mockFileSystemProvider.Object,
                _warningsCountLogger
            );

            // Default setup for file system operations
            _mockFileSystemProvider.Setup(m => m.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystemProvider.Setup(m => m.DirectoryExists(It.IsAny<string>())).Returns(true);
        }

        private MigrateRepoCommandArgs CreateCustomPropertiesMigration(string state = RepositoryMigrationStatus.Succeeded)
        {
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
            _mockGithubApi.Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null))
                .ReturnsAsync(MIGRATION_ID);
            _mockGithubApi.Setup(x => x.GetMigration(MIGRATION_ID))
                .ReturnsAsync((state, GITHUB_REPO, 0, "Migration failed", "https://example.com/migration.log"));

            return new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
                CustomProperties = "{\"environment\":\"production\",\"teams\":[\"platform\",\"security\"],\"unset\":null}"
            };
        }

        [Fact]
        public async Task It_Applies_Custom_Properties_Only_After_Migration_Succeeds()
        {
            var args = CreateCustomPropertiesMigration();
            var sequence = new MockSequence();
            _mockGithubApi.InSequence(sequence).Setup(x => x.GetMigration(MIGRATION_ID))
                .ReturnsAsync((RepositoryMigrationStatus.Succeeded, GITHUB_REPO, 0, null, "https://example.com/migration.log"));
            _mockGithubApi.InSequence(sequence)
                .Setup(x => x.SetRepositoryCustomProperties(GITHUB_ORG, GITHUB_REPO, It.IsAny<JObject>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _handler.Handle(args);

            _mockGithubApi.Verify(x => x.SetRepositoryCustomProperties(GITHUB_ORG, GITHUB_REPO,
                It.Is<JObject>(properties => JToken.DeepEquals(properties, JObject.Parse(args.CustomProperties)))), Times.Once);
            _mockGithubApi.Verify();
            _mockOctoLogger.Verify(x => x.LogSuccess("Custom properties applied successfully."), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task It_Applies_Custom_Properties_When_Uploading_An_Archive(bool generateArchive)
        {
            var args = CreateCustomPropertiesMigration();
            args.ArchiveUrl = null;
            args.AwsBucketName = AWS_BUCKET_NAME;
            args.AwsAccessKey = AWS_ACCESS_KEY_ID;
            args.AwsSecretKey = AWS_SECRET_ACCESS_KEY;
            args.AwsRegion = AWS_REGION;
            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);

            if (generateArchive)
            {
                args.BbsServerUrl = BBS_SERVER_URL;
                args.BbsProject = BBS_PROJECT;
                args.BbsRepo = BBS_REPO;
                args.BbsUsername = BBS_USERNAME;
                args.BbsPassword = BBS_PASSWORD;
                args.SshUser = SSH_USER;
                args.SshPrivateKey = PRIVATE_KEY;
                _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
                _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
                _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
                _mockGithubApi.Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, BBS_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null))
                    .ReturnsAsync(MIGRATION_ID);
            }
            else
            {
                args.ArchivePath = ARCHIVE_PATH;
            }

            args.Validate(_mockOctoLogger.Object);
            await _handler.Handle(args);

            _mockAwsApi.Verify(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>()), Times.Once);
            _mockGithubApi.Verify(x => x.SetRepositoryCustomProperties(GITHUB_ORG, GITHUB_REPO,
                It.Is<JObject>(properties => JToken.DeepEquals(properties, JObject.Parse(args.CustomProperties)))), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        public async Task It_Does_Not_Update_Custom_Properties_When_None_Are_Requested(string customProperties)
        {
            var args = CreateCustomPropertiesMigration();
            args.CustomProperties = customProperties;

            await _handler.Handle(args);

            _mockGithubApi.Verify(x => x.SetRepositoryCustomProperties(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()), Times.Never);
        }

        [Theory]
        [InlineData(RepositoryMigrationStatus.Failed)]
        [InlineData(RepositoryMigrationStatus.FailedValidation)]
        [InlineData("UNKNOWN")]
        public async Task It_Does_Not_Update_Custom_Properties_When_Migration_Fails(string state)
        {
            var args = CreateCustomPropertiesMigration(state);

            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage("Migration failed");

            _mockGithubApi.Verify(x => x.SetRepositoryCustomProperties(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task It_Does_Not_Update_Custom_Properties_When_Target_Repository_Already_Exists(bool existsBeforeMigration)
        {
            var args = CreateCustomPropertiesMigration();
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(existsBeforeMigration);
            _mockGithubApi.Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null))
                .ThrowsAsync(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            if (existsBeforeMigration)
            {
                await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
            }
            else
            {
                await _handler.Handle(args);
            }

            _mockGithubApi.Verify(x => x.SetRepositoryCustomProperties(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JObject>()), Times.Never);
        }

        [Theory]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.UnprocessableEntity)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task It_Reports_A_Property_Update_Failure_Without_Hiding_The_Successful_Migration(HttpStatusCode statusCode) =>
            await AssertPropertyUpdateFailure(new HttpRequestException("API error", null, statusCode));

        [Fact]
        public async Task It_Reports_A_Property_Update_Timeout_Without_Hiding_The_Successful_Migration() =>
            await AssertPropertyUpdateFailure(new TaskCanceledException("The request timed out."));

        [Fact]
        public async Task It_Reports_A_Property_Update_Cancellation_Without_Hiding_The_Successful_Migration() =>
            await AssertPropertyUpdateFailure(new OperationCanceledException("The request was canceled."));

        [Fact]
        public async Task It_Reports_A_Property_Update_Rate_Limit_Without_Hiding_The_Successful_Migration() =>
            await AssertPropertyUpdateFailure(new OctoshiftCliException("Secondary rate limit exceeded. Maximum retries reached."));

        private async Task AssertPropertyUpdateFailure(Exception apiException)
        {
            var args = CreateCustomPropertiesMigration();
            _mockGithubApi.Setup(x => x.SetRepositoryCustomProperties(GITHUB_ORG, GITHUB_REPO, It.IsAny<JObject>()))
                .ThrowsAsync(apiException);

            var exception = await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>()
                .WithMessage($"Migration completed (ID: {MIGRATION_ID}), but custom properties could not be applied to {GITHUB_ORG}/{GITHUB_REPO}.*do not rerun the migration*{apiException.Message}");

            exception.Which.InnerException.Should().BeSameAs(apiException);
            _mockOctoLogger.Verify(x => x.LogSuccess("Custom properties applied successfully."), Times.Never);
        }

        [Fact]
        public async Task It_Does_Not_Wrap_Unexpected_Property_Update_Errors()
        {
            var args = CreateCustomPropertiesMigration();
            var unexpectedException = new InvalidOperationException("Unexpected error");
            _mockGithubApi.Setup(x => x.SetRepositoryCustomProperties(GITHUB_ORG, GITHUB_REPO, It.IsAny<JObject>()))
                .ThrowsAsync(unexpectedException);

            var exception = await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<InvalidOperationException>();

            exception.Which.Should().BeSameAs(unexpectedException);
            _mockOctoLogger.Verify(x => x.LogSuccess("Custom properties applied successfully."), Times.Never);
        }

        [Theory]
        [InlineData("invalid-json", false)]
        [InlineData("{\"environment\":\"production\"}", true)]
        public async Task It_Rejects_Invalid_Custom_Property_Options_Before_Any_Api_Calls(string customProperties, bool queueOnly)
        {
            var args = CreateCustomPropertiesMigration();
            args.CustomProperties = customProperties;
            args.QueueOnly = queueOnly;

            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();

            _mockGithubApi.VerifyNoOtherCalls();
            _mockBbsApi.VerifyNoOtherCalls();
            _mockAzureApi.VerifyNoOtherCalls();
            _mockAwsApi.VerifyNoOtherCalls();
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

            _mockGithubApi.Verify(m => m.DoesRepoExist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
            _mockGithubApi.Verify(m => m.DoesRepoExist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Happy_Path_Ingest_Only()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO).Result).Returns(false);
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
        }

        [Fact]
        public async Task Happy_Path_Generate_Archive_Ssh_Download_Azure_Upload_And_Ingest()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO).Result).Returns(false);
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
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
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                BBS_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
        }

        [Fact]
        public async Task Happy_Path_Generate_Archive_Ssh_Download_Aws_Upload_And_Ingest()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO).Result).Returns(false);
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
                AwsRegion = AWS_REGION,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                BBS_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
        }

        [Fact]
        public async Task Happy_Path_Full_Flow_Running_On_Bbs_Server()
        {
            // Arrange
            const string bbsSharedHome = "bbs-shared-home";
            var archivePath = $"{bbsSharedHome}/data/migration/export/Bitbucket_export_{BBS_EXPORT_ID}.tar";

            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO).Result).Returns(false);
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
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
                BbsSharedHome = bbsSharedHome,
                QueueOnly = true,
            };

            var handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockBbsApi.Object,
                _mockEnvironmentVariableProvider.Object,
                null, // in case of running on Bitbucket server, the downloader will be null
                _mockAzureApi.Object,
                _mockAwsApi.Object,
                _mockFileSystemProvider.Object,
                _warningsCountLogger
            );
            await handler.Handle(args);

            // Assert
            args.ArchivePath.Should().Be(archivePath);

            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                BBS_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
        }

        [Fact]
        public async Task Happy_Path_Full_Flow_Bbs_Credentials_Via_Environment()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.BbsUsername(It.IsAny<bool>())).Returns(BBS_USERNAME);
            _mockEnvironmentVariableProvider.Setup(m => m.BbsPassword(It.IsAny<bool>())).Returns(BBS_PASSWORD);
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO).Result).Returns(false);
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(ARCHIVE_DATA);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
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
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                BBS_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
        }

        [Fact]
        public async Task Happy_Path_Uploads_To_Github_Storage()
        {
            // Arrange
            var githubOrgDatabaseId = Guid.NewGuid().ToString();
            const string gitArchiveFilePath = "./gitdata_archive";
            const string gitArchiveUrl = "gei://archive/1";
            const string gitArchiveContents = "I am git archive";

            await using var gitContentStream = new MemoryStream(gitArchiveContents.ToBytes());

            _mockFileSystemProvider.Setup(m => m.OpenRead(gitArchiveFilePath)).Returns(gitContentStream);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi.Setup(x => x.GetOrganizationDatabaseId(GITHUB_ORG).Result).Returns(githubOrgDatabaseId);
            _mockGithubApi
                .Setup(x => x.UploadArchiveToGithubStorage(
                    githubOrgDatabaseId,
                    It.IsAny<string>(),
                    It.Is<Stream>(s => (s as MemoryStream).ToArray().GetString() == gitArchiveContents)).Result)
                .Returns(gitArchiveUrl);

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
                ArchivePath = gitArchiveFilePath,
                UseGithubStorage = true,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                BBS_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                gitArchiveUrl,
                null
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
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));

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
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
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
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ThrowsAsync(new InvalidOperationException());

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
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
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
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(ARCHIVE_PATH));
        }

        [Fact]
        public async Task Dont_Generate_Archive_If_Target_Repo_Exists()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(true);

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
                GithubPat = GITHUB_PAT,
                QueueOnly = true,
            };

            await FluentActions
                .Invoking(async () => await _handler.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();

            // Assert
            _mockBbsApi.Verify(x => x.StartExport(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Uses_GitHub_Pat_When_Provided_As_Option()
        {
            // Arrange
            var githubPat = "specific github pat";

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = githubPat,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                githubPat,
                ARCHIVE_URL,
                null
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
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null).Result)
                .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
        }

        [Fact]
        public async Task Throws_Decorated_Error_When_Create_Migration_Source_Fails_With_Permissions_Error()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi
                .Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result)
                .Throws(new OctoshiftCliException("monalisa does not have the correct permissions to execute `CreateMigrationSource`"));

            // Act
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage($"monalisa does not have the correct permissions to execute `CreateMigrationSource`. Please check that:\n  (a) you are a member of the `{GITHUB_ORG}` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.");
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
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Uses_Archive_Path_If_Provided()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);

            var archiveBytes = Encoding.ASCII.GetBytes("here are some bytes");
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(archiveBytes);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
            ));
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
        public async Task It_Does_Not_Set_The_Archive_Path_When_Archive_Path_Is_Provided()
        {
            // Arrange
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            args.ArchivePath.Should().Be(ARCHIVE_PATH);
            _mockFileSystemProvider.Verify(m => m.OpenRead(ARCHIVE_PATH));
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
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
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
            _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                ArchivePath = ARCHIVE_PATH,
                AwsAccessKey = AWS_ACCESS_KEY_ID,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsRegion = AWS_REGION,
                QueueOnly = true,
            };

            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL,
                null
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
        public async Task It_Throws_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Region()
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
                .WithMessage("Either --aws-region or AWS_REGION environment variable must be set.");
        }

        [Fact]
        public async Task Errors_If_BbsServer_Url_Provided_But_No_Bbs_Username()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
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
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
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
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                Kerberos = true
            };

            // Assert
            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .NotThrowAsync();
        }

        [Fact]
        public async Task Sets_Target_Repo_Visibility()
        {
            // Arrange
            var targetRepoVisibility = "public";

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
                TargetRepoVisibility = targetRepoVisibility,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                targetRepoVisibility
            ));
        }

        [Fact]
        public async Task It_Throws_When_Archive_Path_Does_Not_Exist()
        {
            const string nonExistentArchivePath = "/path/to/nonexistent/archive.tar";
            _mockFileSystemProvider.Setup(m => m.FileExists(nonExistentArchivePath)).Returns(false);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                ArchivePath = nonExistentArchivePath,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage($"*--archive-path*{nonExistentArchivePath}*");
        }

        [Fact]
        public async Task It_Throws_When_Bbs_Shared_Home_Does_Not_Exist_When_Running_On_Bitbucket_Instance()
        {
            const string nonExistentBbsSharedHome = "/nonexistent/shared/home";
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockFileSystemProvider.Setup(m => m.DirectoryExists(nonExistentBbsSharedHome)).Returns(false);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsSharedHome = nonExistentBbsSharedHome
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage($"*--bbs-shared-home*{nonExistentBbsSharedHome}*");
        }

        [Fact]
        public async Task It_Does_Not_Validate_Bbs_Shared_Home_When_Using_Ssh()
        {
            const string nonExistentBbsSharedHome = "/nonexistent/shared/home";
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(m => m.DirectoryExists(nonExistentBbsSharedHome)).Returns(false);

            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsSharedHome = nonExistentBbsSharedHome,
                SshUser = SSH_USER,
                SshPrivateKey = PRIVATE_KEY
            };

            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .NotThrowAsync();
        }

        [Fact]
        public async Task It_Does_Not_Validate_Bbs_Shared_Home_When_Using_Smb()
        {
            const string nonExistentBbsSharedHome = "/nonexistent/shared/home";
            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));
            _mockBbsArchiveDownloader.Setup(x => x.Download(BBS_EXPORT_ID, It.IsAny<string>())).ReturnsAsync(ARCHIVE_PATH);
            _mockFileSystemProvider.Setup(m => m.DirectoryExists(nonExistentBbsSharedHome)).Returns(false);
            _mockEnvironmentVariableProvider.Setup(m => m.SmbPassword(It.IsAny<bool>())).Returns(SMB_PASSWORD);

            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                BbsSharedHome = nonExistentBbsSharedHome,
                SmbUser = SMB_USER
            };

            await _handler.Invoking(x => x.Handle(args))
                .Should()
                .NotThrowAsync();
        }

        [Fact]
        public async Task It_Logs_Archive_Path_Before_Upload()
        {
            _mockFileSystemProvider.Setup(m => m.FileExists(ARCHIVE_PATH)).Returns(true);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>())).ReturnsAsync(new Uri(ARCHIVE_URL));

            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                QueueOnly = true,
            };

            await _handler.Handle(args);
            _mockOctoLogger.Verify(m => m.LogInformation($"Archive path: {ARCHIVE_PATH}"), Times.Once);
        }
    }
}
