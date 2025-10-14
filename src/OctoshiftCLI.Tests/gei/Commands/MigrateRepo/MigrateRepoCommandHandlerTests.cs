using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateRepo;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateRepo
{
    public class MigrateRepoCommandHandlerTests
    {
        private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<AzureApi> _mockAzureApi = TestHelpers.CreateMock<AzureApi>();
        private readonly Mock<AwsApi> _mockAwsApi = TestHelpers.CreateMock<AwsApi>();
        private readonly Mock<HttpDownloadService> _mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
        private readonly Mock<GhesVersionChecker> _mockGhesVersionChecker = TestHelpers.CreateMock<GhesVersionChecker>();

        private readonly RetryPolicy _retryPolicy;
        private readonly MigrateRepoCommandHandler _handler;
        private readonly WarningsCountLogger _warningsCountLogger;

        private const string TARGET_API_URL = "https://api.github.com";
        private const string GHES_API_URL = "https://myghes/api/v3";
        private const string AZURE_CONNECTION_STRING = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
        private const string SOURCE_ORG = "foo-source-org";
        private const string SOURCE_REPO = "foo-repo-source";
        private const string TARGET_ORG = "foo-target-org";
        private const string TARGET_REPO = "foo-target-repo";
        private const string GITHUB_TARGET_PAT = "github-target-pat";
        private const string GITHUB_SOURCE_PAT = "github-source-pat";
        private const string AWS_BUCKET_NAME = "aws-bucket-name";
        private const string AWS_ACCESS_KEY_ID = "aws-access-key-id";
        private const string AWS_SECRET_ACCESS_KEY = "aws-secret-access-key";
        private const string AWS_SESSION_TOKEN = "aws-session-token";
        private const string AWS_REGION = "aws-region";
        private const string GIT_ARCHIVE_FILE_NAME = "git_archive.tar.gz";
        private const string METADATA_ARCHIVE_FILE_NAME = "metadata_archive.tar.gz";
        private const string GITHUB_ORG_ID = "9b1b5862-6a8b-4fe1-8cef-0f4e67e975e8";
        private const string MIGRATION_SOURCE_ID = "b6ec2c17-c4d7-4552-83f6-b5407153c324";
        private const string GITHUB_REPO_URL = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
        private const string GHES_REPO_URL = $"https://myghes/{SOURCE_ORG}/{SOURCE_REPO}";
        private const string MIGRATION_ID = "069f660d-9201-47c5-95d4-f9b743cb89d9";
        private const int GIT_ARCHIVE_ID = 1;
        private const int METADATA_ARCHIVE_ID = 2;
        private const string GIT_ARCHIVE_URL = "https://example.com/1";
        private const string METADATA_ARCHIVE_URL = "https://example.com/2";
        private const string AUTHENTICATED_GIT_ARCHIVE_URL = $"https://example.com/1/authenticated";
        private const string AUTHENTICATED_METADATA_ARCHIVE_URL = $"https://example.com/2/authenticated";
        private const string GIT_ARCHIVE_FILE_PATH = "path/to/git_archive";
        private const string METADATA_ARCHIVE_FILE_PATH = "path/to/metadata_archive";

        public MigrateRepoCommandHandlerTests()
        {
            _retryPolicy = new RetryPolicy(_mockOctoLogger.Object) { _httpRetryInterval = 1, _retryInterval = 0 };
            _warningsCountLogger = new WarningsCountLogger(_mockOctoLogger.Object);

            _handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockSourceGithubApi.Object,
                _mockTargetGithubApi.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockAzureApi.Object,
                null,
                _mockHttpDownloadService.Object,
                _mockFileSystemProvider.Object,
                _mockGhesVersionChecker.Object,
                _retryPolicy,
                _warningsCountLogger);
        }

        [Fact]
        public async Task Dont_Generate_Archives_If_Target_Repo_Exists()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.DoesRepoExist(TARGET_ORG, TARGET_REPO)).ReturnsAsync(true);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
            };
            await FluentActions
                .Invoking(async () => await _handler.Handle(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();

            // Assert
            _mockSourceGithubApi.Verify(x => x.StartGitArchiveGeneration(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Happy_Path_Without_Wait()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.StartMigration(MIGRATION_SOURCE_ID, GITHUB_REPO_URL, GITHUB_ORG_ID, TARGET_REPO, GITHUB_SOURCE_PAT, GITHUB_TARGET_PAT, null, null, false, null, false).Result).Returns(MIGRATION_ID);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Repo...",
                $"A repository migration (ID: {MIGRATION_ID}) was successfully queued."
            };

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG));
            _mockTargetGithubApi.Verify(m => m.CreateGhecMigrationSource(GITHUB_ORG_ID));
            _mockTargetGithubApi.Verify(m => m.StartMigration(MIGRATION_SOURCE_ID, GITHUB_REPO_URL, GITHUB_ORG_ID, TARGET_REPO, GITHUB_SOURCE_PAT, GITHUB_TARGET_PAT, null, null, false, null, false));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(2));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockTargetGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(MIGRATION_SOURCE_ID, GITHUB_REPO_URL, GITHUB_ORG_ID, TARGET_REPO, GITHUB_SOURCE_PAT, GITHUB_TARGET_PAT, null, null, false, null, false).Result)
                .Throws(new OctoshiftCliException($"A repository called {TARGET_ORG}/{TARGET_REPO} already exists"));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = $"The Org '{TARGET_ORG}' already contains a repository with the name '{TARGET_REPO}'. No operation will be performed";

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Contain(expectedLogOutput);
        }

        [Fact]
        public async Task Happy_Path_GithubSource()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GITHUB_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    null,
                    null,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task Throws_Decorated_Error_When_Create_Migration_Source_Fails_With_Permissions_Error()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi
                .Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result)
                .Throws(new OctoshiftCliException("monalisa does not have the correct permissions to execute `CreateMigrationSource`"));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            // Act
            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                QueueOnly = true,
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage($"monalisa does not have the correct permissions to execute `CreateMigrationSource`. Please check that:\n  (a) you are a member of the `{TARGET_ORG}` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.");
        }

        [Fact]
        public async Task Happy_Path_GithubSource_Ghes()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Happy_Path_UseGithubStorage(bool useGhesBlobCredentials)
        {
            var githubOrgDatabaseId = Guid.NewGuid().ToString();
            var uploadedGitArchiveUrl = "gei://archive/1";
            var uploadedMetadataArchiveUrl = "gei://archive/2";
            var gitArchiveDownloadFilePath = "git_archive_downaloded.tmp";
            var metadataArchiveDownloadFilePath = "metadata_archive_downloaded.tmp";
            var gitArchiveContents = "I am git archive";
            var metadataArchiveContents = "I am metadata archive";

            using var gitContentStream = new MemoryStream(gitArchiveContents.ToBytes());
            using var metaContentStream = new MemoryStream(metadataArchiveContents.ToBytes());

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(gitArchiveDownloadFilePath)
                .Returns(metadataArchiveDownloadFilePath);

            _mockFileSystemProvider
                .Setup(m => m.OpenRead(gitArchiveDownloadFilePath))
                .Returns(gitContentStream);

            _mockFileSystemProvider
                .Setup(m => m.OpenRead(metadataArchiveDownloadFilePath))
                .Returns(metaContentStream);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(useGhesBlobCredentials);

            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.GetOrganizationDatabaseId(TARGET_ORG).Result).Returns(githubOrgDatabaseId);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                uploadedGitArchiveUrl,
                uploadedMetadataArchiveUrl,
                false,
                null,
                false).Result)
                .Returns(MIGRATION_ID);

            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result)
                .Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockTargetGithubApi
                .Setup(x => x.UploadArchiveToGithubStorage(
                    githubOrgDatabaseId,
                    It.Is<string>(a => a.EndsWith("git_archive.tar.gz")),
                    It.Is<Stream>(s => (s as MemoryStream).ToArray().GetString() == gitArchiveContents)).Result)
                .Returns(uploadedGitArchiveUrl);

            _mockTargetGithubApi
                .Setup(x => x.UploadArchiveToGithubStorage(
                    githubOrgDatabaseId,
                    It.Is<string>(a => a.EndsWith("metadata_archive.tar.gz")),
                    It.Is<Stream>(s => (s as MemoryStream).ToArray().GetString() == metadataArchiveContents)).Result)
                .Returns(uploadedMetadataArchiveUrl);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GithubSourcePat = GITHUB_SOURCE_PAT,
                GithubTargetPat = GITHUB_TARGET_PAT,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                UseGithubStorage = true,
            };

            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(gitArchiveDownloadFilePath), Times.Once);
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(metadataArchiveDownloadFilePath), Times.Once);
        }

        [Fact]
        public async Task Happy_Path_GithubSource_Ghes_Repo_Renamed()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Github_With_Archive_Urls()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GITHUB_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    GIT_ARCHIVE_URL,
                    METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GitArchiveUrl = GIT_ARCHIVE_URL,
                MetadataArchiveUrl = METADATA_ARCHIVE_URL
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task With_Archive_Paths()
        {
            var gitArchivePath = $"/path/{GIT_ARCHIVE_FILE_NAME}";
            var metadataArchivePath = $"/path/{METADATA_ARCHIVE_FILE_NAME}";

            _mockAzureApi
                .Setup(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(GIT_ARCHIVE_FILE_NAME)), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(GIT_ARCHIVE_URL));
            _mockAzureApi
                .Setup(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(METADATA_ARCHIVE_FILE_NAME)), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(METADATA_ARCHIVE_URL));

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GITHUB_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    GIT_ARCHIVE_URL,
                    METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GitArchivePath = gitArchivePath,
                MetadataArchivePath = metadataArchivePath,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));

            _mockAzureApi.Verify(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(GIT_ARCHIVE_FILE_NAME)), It.IsAny<FileStream>()), Times.Once());
            _mockAzureApi.Verify(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(METADATA_ARCHIVE_FILE_NAME)), It.IsAny<FileStream>()), Times.Once());
            _mockAzureApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task With_Duplicate_Archive_Paths()
        {
            var gitArchivePath = $"/path/{GIT_ARCHIVE_FILE_NAME}";
            var metadataArchivePath = $"/path/{GIT_ARCHIVE_FILE_NAME}";
            var duplicateArchiveFileName = "archive.tar.gz";

            _mockAzureApi
                .Setup(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(duplicateArchiveFileName)), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(GIT_ARCHIVE_URL));

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GITHUB_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    GIT_ARCHIVE_URL,
                    GIT_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GitArchivePath = gitArchivePath,
                MetadataArchivePath = metadataArchivePath,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));

            _mockAzureApi.Verify(x => x.UploadToBlob(It.Is<string>(s => s.EndsWith(duplicateArchiveFileName)), It.IsAny<FileStream>()), Times.Once());
            _mockAzureApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Github_Only_One_Archive_Url_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Handle(new MigrateRepoCommandArgs
                {
                    GithubSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = TARGET_API_URL,
                    GitArchiveUrl = GIT_ARCHIVE_URL,
                }))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ghes_Without_AzureConnectionString_Or_Aws_Bucket_Name_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Handle(new MigrateRepoCommandArgs
                {
                    GithubSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    GhesApiUrl = GHES_API_URL
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ghes_AzureConnectionString_Uses_Env_When_Option_Empty()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.AzureStorageConnectionString(It.IsAny<bool>())).Returns(AZURE_CONNECTION_STRING);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task Ghes_With_NoSslVerify_Uses_NoSsl_Client()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                NoSslVerify = true,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task Ghes_With_3_8_0_Version_Returns_Archive_Urls_Directly()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);

            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    GIT_ARCHIVE_URL,
                    METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(false);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                NoSslVerify = true,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task Ghes_Failed_Archive_Generation_Throws_Error()
        {
            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Failed);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await FluentActions
                .Invoking(async () => await _handler.Handle(new MigrateRepoCommandArgs
                {
                    GithubSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = TARGET_API_URL,
                    GhesApiUrl = GHES_API_URL,
                    AzureStorageConnectionString = AZURE_CONNECTION_STRING
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ghes_Retries_Archive_Generation_On_Any_Error()
        {
            _mockSourceGithubApi.Setup(x => x.GetEnterpriseServerVersion()).ReturnsAsync("3.7.1");
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi
                .SetupSequence(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result)
                .Throws<TimeoutException>()
                .Returns(GIT_ARCHIVE_ID)
                .Returns(GIT_ARCHIVE_ID) // for first StartMetadataArchiveGeneration throw
                .Returns(GIT_ARCHIVE_ID) // for second StartMetadataArchiveGeneration throw
                .Returns(GIT_ARCHIVE_ID) // for GetArchiveMigrationStatus Failed
                .Returns(GIT_ARCHIVE_ID); // for GetArchiveMigrationStatus TimeoutException
            _mockSourceGithubApi
                .SetupSequence(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result)
                .Throws<HttpRequestException>()
                .Throws<OctoshiftCliException>()
                .Returns(METADATA_ARCHIVE_ID)
                .Returns(METADATA_ARCHIVE_ID) // for GetArchiveMigrationStatus Failed
                .Returns(METADATA_ARCHIVE_ID); // for GetArchiveMigrationStatus TimeoutException
            _mockSourceGithubApi
                .SetupSequence(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result)
                .Returns(ArchiveMigrationStatus.Failed)
                .Returns(ArchiveMigrationStatus.Exported)
                .Returns(ArchiveMigrationStatus.Exported); // for GetArchiveMigrationStatus TimeoutException
            _mockSourceGithubApi
                .SetupSequence(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result)
                .Throws<TimeoutException>()
                .Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            _mockTargetGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task It_Uses_Github_Source_And_Target_Pats_When_Provided()
        {
            // Arrange
            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GithubTargetPat = GITHUB_TARGET_PAT,
                GithubSourcePat = GITHUB_SOURCE_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            actualLogOutput.Should().NotContain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
            _mockTargetGithubApi.Verify(m => m.CreateGhecMigrationSource(It.IsAny<string>()));
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Uses_Github_Source_Pat_When_Provided()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockAzureApi.Setup(m => m.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://example.com/resource"));

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                GithubSourcePat = GITHUB_SOURCE_PAT,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            actualLogOutput.Should().NotContain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()));
            _mockTargetGithubApi.Verify(m => m.CreateGhecMigrationSource(It.IsAny<string>()));
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                GITHUB_SOURCE_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Skips_Releases_When_Option_Is_True()
        {
            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                SkipReleases = true,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<string>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Locks_Source_Repo_When_Option_Is_True()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                SkipReleases = false,
                LockSourceRepo = true,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                true));
        }

        [Fact]
        public async Task Does_Not_Pass_Lock_Repos_To_StartMigration_For_GHES()
        {
            // Arrange
            _mockSourceGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://example.com/resource"));
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true,
                QueueOnly = true,
                LockSourceRepo = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                false));
        }

        [Fact]
        public async Task It_Skips_Releases_When_Option_Is_True_For_Ghes_Migration_Path()
        {
            // Arrange
            _mockSourceGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://example.com/resource"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockSourceGithubApi.Verify(m => m.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, true, false));
        }

        [Fact]
        public async Task It_Locks_Repos_When_Option_Is_True_For_Ghes_Migration_Path()
        {
            // Arrange
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockHttpDownloadService.Setup(x => x.DownloadToBytes(It.IsAny<string>()).Result).Returns(Array.Empty<byte>());
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://example.com/resource"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true,
                LockSourceRepo = true,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockSourceGithubApi.Verify(m => m.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, true, true));
        }

        [Fact]
        public async Task It_Extracts_Base_Ghes_Url_From_Ghes_Api_Url_Using_Alternate_Template()
        {
            // Arrange
            const string ghesApiUrl = "https://api.myghes.com";
            var expectedGITHUB_REPO_URL = $"https://myghes.com/{SOURCE_ORG}/{SOURCE_REPO}";

            _mockSourceGithubApi
                .Setup(x => x.StartMigration(
                    It.IsAny<string>(),
                    expectedGITHUB_REPO_URL,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<string>(),
                    false).Result)
                .Returns("MIGRATION_ID");

            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://blob-url"));

            // act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                expectedGITHUB_REPO_URL,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<string>(),
                false));
        }

        [Fact]
        public async Task It_Falls_Back_To_The_Ghes_Api_Url_If_Could_Not_Extract_Base_Ghes_Url()
        {
            // Arrange
            const string ghesApiUrl = "https://non-conforming-ghes-api-url";
            var expectedGITHUB_REPO_URL = $"{ghesApiUrl}/{SOURCE_ORG}/{SOURCE_REPO}";

            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    It.IsAny<string>(),
                    expectedGITHUB_REPO_URL,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    It.IsAny<string>(),
                    false).Result)
                .Returns("MIGRATION_ID");

            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>()).Result).Returns(new Uri("https://blob-url"));

            // act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                QueueOnly = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                expectedGITHUB_REPO_URL,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                It.IsAny<string>(),
                false));
        }

        [Fact]
        public async Task It_Uses_Aws_If_Arguments_Are_Included()
        {
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };

            var awsAccessKeyId = "awsAccessKeyId";
            var awsSecretAccessKey = "awsSecretAccessKey";
            var awsBucketName = "awsBucketName";
            var awsRegion = "eu-west-1";
            var archiveUrl = $"https://s3.amazonaws.com/{awsBucketName}/archive.tar";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    archiveUrl,
                    archiveUrl,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockHttpDownloadService.Setup(x => x.DownloadToBytes(GIT_ARCHIVE_URL).Result).Returns(gitArchiveContent);
            _mockHttpDownloadService.Setup(x => x.DownloadToBytes(METADATA_ARCHIVE_URL).Result).Returns(metadataArchiveContent);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockAwsApi.Setup(m => m.UploadToBucket(awsBucketName, It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync(archiveUrl);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockSourceGithubApi.Object,
                _mockTargetGithubApi.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockAzureApi.Object,
                _mockAwsApi.Object,
                _mockHttpDownloadService.Object,
                _mockFileSystemProvider.Object,
                _mockGhesVersionChecker.Object,
                _retryPolicy,
                _warningsCountLogger);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AwsBucketName = awsBucketName,
                AwsAccessKey = awsAccessKeyId,
                AwsSecretKey = awsSecretAccessKey,
                AwsRegion = awsRegion,
            };

            await handler.Handle(args);

            // Assert
            _mockAwsApi.Verify(m => m.UploadToBucket(awsBucketName, It.IsAny<Stream>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task Ghes_With_Both_Azure_Storage_Connection_String_And_Aws_Bucket_Name_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsBucketName = AWS_BUCKET_NAME
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Access_Key_Id_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-access-key*");
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Secret_Key_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-secret-key*");
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Region_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
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
        public async Task Ghes_When_Aws_Bucket_Name_Not_Provided_But_Aws_Access_Key_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Not_Provided_But_Aws_Secret_Key_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Not_Provided_But_Aws_Session_Token_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsSessionToken = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task Ghes_When_Aws_Bucket_Name_Not_Provided_But_Aws_Region_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsRegion = AWS_REGION
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task GitArchivePath_With_Both_Azure_Storage_Connection_String_And_Aws_Bucket_Name_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsBucketName = AWS_BUCKET_NAME
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Access_Key_Id_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-access-key*");
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Secret_Key_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AwsBucketName = AWS_BUCKET_NAME,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*--aws-secret-key*");
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Is_Provided_But_No_Aws_Region_Throws()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
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
        public async Task GitArchivePath_When_Aws_Bucket_Name_Not_Provided_But_Aws_Access_Key_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsAccessKey = AWS_ACCESS_KEY_ID
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Not_Provided_But_Aws_Secret_Key_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsSecretKey = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Not_Provided_But_Aws_Session_Token_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsSessionToken = AWS_SECRET_ACCESS_KEY
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task GitArchivePath_When_Aws_Bucket_Name_Not_Provided_But_Aws_Region_Provided()
        {
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            await _handler.Invoking(async x => await x.Handle(new MigrateRepoCommandArgs
            {
                SourceRepo = SOURCE_REPO,
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GitArchivePath = GIT_ARCHIVE_FILE_PATH,
                MetadataArchivePath = METADATA_ARCHIVE_FILE_PATH,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                AwsRegion = AWS_REGION
            }))
                .Should()
                .ThrowAsync<OctoshiftCliException>()
                .WithMessage("*AWS S3*--aws-bucket-name*");
        }

        [Fact]
        public async Task Keep_Archive_Does_Not_Call_DeleteIfExists_And_Logs_Downloaded_Archive_Paths()
        {
            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    GHES_REPO_URL,
                    GITHUB_ORG_ID,
                    TARGET_REPO,
                    GITHUB_SOURCE_PAT,
                    GITHUB_TARGET_PAT,
                    AUTHENTICATED_GIT_ARCHIVE_URL,
                    AUTHENTICATED_METADATA_ARCHIVE_URL,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, "", ""));
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);

            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                KeepArchive = true
            };
            await _handler.Handle(args);

            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(GIT_ARCHIVE_FILE_PATH), Times.Never);
            _mockFileSystemProvider.Verify(x => x.DeleteIfExists(METADATA_ARCHIVE_FILE_PATH), Times.Never);

            _mockOctoLogger.Verify(x => x.LogInformation($"Git archive was successfully downloaded at \"{GIT_ARCHIVE_FILE_PATH}\""));
            _mockOctoLogger.Verify(x => x.LogInformation($"Metadata archive was successfully downloaded at \"{METADATA_ARCHIVE_FILE_PATH}\""));
        }

        [Fact]
        public async Task Sets_Target_Repo_Visibility()
        {
            // Arrange
            var targetRepoVisibility = "internal";

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                QueueOnly = true,
                TargetRepoVisibility = targetRepoVisibility,
            };
            await _handler.Handle(args);

            // Assert
            _mockTargetGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                targetRepoVisibility,
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task Git_Archive_Download_Retries_On_403_Error()
        {
            // Arrange
            var freshGitArchiveUrl = "https://example.com/1/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            // Setup second call to GetArchiveMigrationUrl for git archive to return fresh URL
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result)
                .Returns(GIT_ARCHIVE_URL)  // First call during archive generation
                .Returns(freshGitArchiveUrl); // Second call during retry

            // Setup HttpDownloadService to fail first time with 403, succeed second time
            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), GIT_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpForbiddenException())
                .Returns(Task.CompletedTask);

            _mockHttpDownloadService
                .Setup(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH))
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            // Verify that GetArchiveMigrationUrl was called twice for git archive (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID), Times.Exactly(2));

            // Verify that DownloadToFile was called twice for git archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshGitArchiveUrl, GIT_ARCHIVE_FILE_PATH), Times.Once);

            // Verify metadata archive was only downloaded once (no retry needed)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Metadata_Archive_Download_Retries_On_403_Error()
        {
            // Arrange
            var freshMetadataArchiveUrl = "https://example.com/2/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            // Setup second call to GetArchiveMigrationUrl for metadata archive to return fresh URL
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result)
                .Returns(METADATA_ARCHIVE_URL)  // First call during archive generation
                .Returns(freshMetadataArchiveUrl); // Second call during retry

            // Setup HttpDownloadService - git archive succeeds, metadata archive fails first time with 403, succeeds second time
            _mockHttpDownloadService
                .Setup(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH))
                .Returns(Task.CompletedTask);

            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), METADATA_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpForbiddenException())
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            // Verify that GetArchiveMigrationUrl was called twice for metadata archive (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID), Times.Exactly(2));

            // Verify that DownloadToFile was called twice for metadata archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshMetadataArchiveUrl, METADATA_ARCHIVE_FILE_PATH), Times.Once);

            // Verify git archive was only downloaded once (no retry needed)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Both_Archives_Download_Retry_On_403_Error()
        {
            // Arrange
            var freshGitArchiveUrl = "https://example.com/1/fresh";
            var freshMetadataArchiveUrl = "https://example.com/2/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);

            // Setup GetArchiveMigrationUrl to return original URLs first, then fresh URLs on retry
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result)
                .Returns(GIT_ARCHIVE_URL)
                .Returns(freshGitArchiveUrl);

            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result)
                .Returns(METADATA_ARCHIVE_URL)
                .Returns(freshMetadataArchiveUrl);

            // Setup HttpDownloadService to fail first time for both archives with 403, succeed on retry
            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), GIT_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpForbiddenException())
                .Returns(Task.CompletedTask);

            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), METADATA_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpForbiddenException())
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            // Verify that GetArchiveMigrationUrl was called twice for both archives (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID), Times.Exactly(2));
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID), Times.Exactly(2));

            // Verify that DownloadToFile was called twice for each archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshGitArchiveUrl, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshMetadataArchiveUrl, METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Git_Archive_Download_Retries_On_404_Error()
        {
            // Arrange
            var freshGitArchiveUrl = "https://example.com/1/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);

            // Setup GetArchiveMigrationUrl to return original URL first, then fresh URL on retry
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result)
                .Returns(GIT_ARCHIVE_URL)
                .Returns(freshGitArchiveUrl);

            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(METADATA_ARCHIVE_URL);

            // Setup HttpDownloadService to fail first time with 404, succeed on retry
            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), GIT_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpNotFoundException())
                .Returns(Task.CompletedTask);

            _mockHttpDownloadService
                .Setup(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH))
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            // Verify that GetArchiveMigrationUrl was called twice for git archive (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID), Times.Exactly(2));
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID), Times.Once);

            // Verify that DownloadToFile was called twice for git archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshGitArchiveUrl, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Metadata_Archive_Download_Retries_On_404_Error()
        {
            // Arrange
            var freshMetadataArchiveUrl = "https://example.com/2/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(GIT_ARCHIVE_URL);

            // Setup GetArchiveMigrationUrl to return original URL first, then fresh URL on retry
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result)
                .Returns(METADATA_ARCHIVE_URL)
                .Returns(freshMetadataArchiveUrl);

            _mockHttpDownloadService
                .Setup(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH))
                .Returns(Task.CompletedTask);

            // Setup HttpDownloadService to fail first time with 404, succeed on retry
            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), METADATA_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpNotFoundException())
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID), Times.Once);
            // Verify that GetArchiveMigrationUrl was called twice for metadata archive (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID), Times.Exactly(2));

            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
            // Verify that DownloadToFile was called twice for metadata archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshMetadataArchiveUrl, METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        [Fact]
        public async Task Both_Archives_Download_Retry_On_404_Error()
        {
            // Arrange
            var freshGitArchiveUrl = "https://example.com/1/fresh";
            var freshMetadataArchiveUrl = "https://example.com/2/fresh";

            _mockTargetGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockTargetGithubApi.Setup(x => x.CreateGhecMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockTargetGithubApi.Setup(x => x.DoesOrgExist(TARGET_ORG).Result).Returns(true);
            _mockTargetGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                GHES_REPO_URL,
                GITHUB_ORG_ID,
                TARGET_REPO,
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                AUTHENTICATED_GIT_ARCHIVE_URL,
                AUTHENTICATED_METADATA_ARCHIVE_URL,
                false,
                null,
                false).Result).Returns(MIGRATION_ID);
            _mockTargetGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, 0, null, null));

            _mockSourceGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(GIT_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(METADATA_ARCHIVE_ID);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, GIT_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockSourceGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, METADATA_ARCHIVE_ID).Result).Returns(ArchiveMigrationStatus.Exported);

            // Setup GetArchiveMigrationUrl to return original URLs first, then fresh URLs on retry
            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID).Result)
                .Returns(GIT_ARCHIVE_URL)
                .Returns(freshGitArchiveUrl);

            _mockSourceGithubApi.SetupSequence(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID).Result)
                .Returns(METADATA_ARCHIVE_URL)
                .Returns(freshMetadataArchiveUrl);

            // Setup HttpDownloadService to fail first time for both archives with 404, succeed on retry
            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), GIT_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpNotFoundException())
                .Returns(Task.CompletedTask);

            _mockHttpDownloadService
                .SetupSequence(x => x.DownloadToFile(It.IsAny<string>(), METADATA_ARCHIVE_FILE_PATH))
                .ThrowsAsync(CreateHttpNotFoundException())
                .Returns(Task.CompletedTask);

            _mockAzureApi
                .SetupSequence(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<FileStream>()).Result)
                .Returns(new Uri(AUTHENTICATED_GIT_ARCHIVE_URL))
                .Returns(new Uri(AUTHENTICATED_METADATA_ARCHIVE_URL));

            _mockFileSystemProvider
                .SetupSequence(m => m.GetTempFileName())
                .Returns(GIT_ARCHIVE_FILE_PATH)
                .Returns(METADATA_ARCHIVE_FILE_PATH);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_SOURCE_PAT);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_TARGET_PAT);
            _mockGhesVersionChecker.Setup(m => m.AreBlobCredentialsRequired(GHES_API_URL)).ReturnsAsync(true);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
            };
            await _handler.Handle(args);

            // Assert
            // Verify that GetArchiveMigrationUrl was called twice for both archives (once during generation, once during retry)
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, GIT_ARCHIVE_ID), Times.Exactly(2));
            _mockSourceGithubApi.Verify(x => x.GetArchiveMigrationUrl(SOURCE_ORG, METADATA_ARCHIVE_ID), Times.Exactly(2));

            // Verify that DownloadToFile was called twice for each archive (original URL failed, fresh URL succeeded)
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(GIT_ARCHIVE_URL, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshGitArchiveUrl, GIT_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(METADATA_ARCHIVE_URL, METADATA_ARCHIVE_FILE_PATH), Times.Once);
            _mockHttpDownloadService.Verify(x => x.DownloadToFile(freshMetadataArchiveUrl, METADATA_ARCHIVE_FILE_PATH), Times.Once);
        }

        private static HttpRequestException CreateHttpForbiddenException()
        {
            // Use the constructor that sets the StatusCode property (available in .NET 5+)
            return new HttpRequestException("Response status code does not indicate success: 403 (Forbidden).", null, HttpStatusCode.Forbidden);
        }

        private static HttpRequestException CreateHttpNotFoundException()
        {
            // Use the constructor that sets the StatusCode property (available in .NET 5+)
            return new HttpRequestException("Response status code does not indicate success: 404 (Not Found).", null, HttpStatusCode.NotFound);
        }
    }
}
