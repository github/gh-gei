using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
    private readonly Mock<AzureApi> _mockAzureApi = TestHelpers.CreateMock<AzureApi>();
    private readonly Mock<AwsApi> _mockAwsApi = TestHelpers.CreateMock<AwsApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

    private readonly WarningsCountLogger _warningsCountLogger;
    private readonly MigrateRepoCommandHandler _handler;

    private const string ARCHIVE_PATH = "/tmp/gitlab-archive.tar";
    private const string ARCHIVE_URL = "https://archive-url/gitlab-archive.tar";
    private const string GITHUB_ORG = "target-org";
    private const string GITHUB_REPO = "target-repo";
    private const string GITHUB_PAT = "github-pat";
    private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";
    private const string AWS_BUCKET_NAME = "aws-bucket-name";
    private const string AWS_ACCESS_KEY_ID = "aws-access-key-id";
    private const string AWS_SECRET_ACCESS_KEY = "aws-secret-access-key";
    private const string AWS_REGION = "eu-west-1";

    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_PAT = "gitlab-pat";
    private const string GITLAB_GROUP = "gitlab-group";
    private const string GITLAB_PROJECT = "gitlab-project";
    private const string GITLAB_PROJECT_URL = $"{GITLAB_SERVER_URL}/{GITLAB_GROUP}/{GITLAB_PROJECT}";
    private const string UNUSED_REPO_URL = "https://not-used";

    private const string GITHUB_ORG_ID = "github-org-id";
    private const string MIGRATION_SOURCE_ID = "migration-source-id";
    private const string MIGRATION_ID = "migration-id";

    public MigrateRepoCommandHandlerTests()
    {
        _warningsCountLogger = new WarningsCountLogger(_mockOctoLogger.Object);
        _handler = new MigrateRepoCommandHandler(
            _mockOctoLogger.Object,
            _mockGithubApi.Object,
            _mockGitlabApi.Object,
            _mockEnvironmentVariableProvider.Object,
            _mockAzureApi.Object,
            _mockAwsApi.Object,
            _mockFileSystemProvider.Object,
            _warningsCountLogger
        );

        _mockFileSystemProvider.Setup(m => m.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemProvider.Setup(m => m.GetTempFileName()).Returns(ARCHIVE_PATH);
    }

    [Fact]
    public async Task Throws_If_Args_Is_Null()
    {
        await FluentActions
            .Invoking(() => _handler.Handle(null))
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Throws_If_Target_Repo_Already_Exists()
    {
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(true);

        var args = new MigrateRepoCommandArgs
        {
            ArchiveUrl = ARCHIVE_URL,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
        };

        await FluentActions
            .Invoking(() => _handler.Handle(args))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists");
    }

    [Fact]
    public async Task Generate_Only_Calls_Start_Export_And_Downloads()
    {
        _mockGitlabApi.Setup(x => x.GetExport(GITLAB_GROUP, GITLAB_PROJECT))
            .ReturnsAsync(("finished", ARCHIVE_URL));

        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            GitlabGroup = GITLAB_GROUP,
            GitlabProject = GITLAB_PROJECT,
        };

        await _handler.Handle(args);

        _mockGitlabApi.Verify(m => m.StartExport(GITLAB_GROUP, GITLAB_PROJECT));
        _mockGitlabApi.Verify(m => m.DownloadExportArchive(GITLAB_GROUP, GITLAB_PROJECT, It.IsAny<string>()));
        _mockGithubApi.Verify(m => m.DoesRepoExist(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Ingest_Only_Starts_Gitlab_Migration_With_Unused_Source_Url()
    {
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(false);
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateGitlabMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
        _mockGithubApi.Setup(x => x.StartGitlabMigration(MIGRATION_SOURCE_ID, UNUSED_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null))
            .ReturnsAsync(MIGRATION_ID);

        var args = new MigrateRepoCommandArgs
        {
            ArchiveUrl = ARCHIVE_URL,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            QueueOnly = true,
        };

        await _handler.Handle(args);

        _mockGithubApi.Verify(m => m.StartGitlabMigration(
            MIGRATION_SOURCE_ID,
            UNUSED_REPO_URL,
            GITHUB_ORG_ID,
            GITHUB_REPO,
            GITHUB_PAT,
            ARCHIVE_URL,
            null));
    }

    [Fact]
    public async Task Passes_Gitlab_Project_Url_When_All_Gitlab_Args_Provided()
    {
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(false);
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateGitlabMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
        _mockGithubApi.Setup(x => x.StartGitlabMigration(MIGRATION_SOURCE_ID, GITLAB_PROJECT_URL, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL, null))
            .ReturnsAsync(MIGRATION_ID);
        _mockGitlabApi.Setup(x => x.GetExport(GITLAB_GROUP, GITLAB_PROJECT)).ReturnsAsync(("finished", ARCHIVE_URL));
        _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
        using var archiveStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _mockFileSystemProvider.Setup(m => m.OpenRead(ARCHIVE_PATH)).Returns(archiveStream);

        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            GitlabGroup = GITLAB_GROUP,
            GitlabProject = GITLAB_PROJECT,
            ArchivePath = ARCHIVE_PATH,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            GithubPat = GITHUB_PAT,
            QueueOnly = true,
        };

        await _handler.Handle(args);

        _mockGithubApi.Verify(m => m.StartGitlabMigration(
            MIGRATION_SOURCE_ID,
            GITLAB_PROJECT_URL,
            GITHUB_ORG_ID,
            GITHUB_REPO,
            GITHUB_PAT,
            ARCHIVE_URL,
            null));
    }

    [Fact]
    public async Task Uploads_To_Aws_When_Aws_Bucket_Name_Provided()
    {
        _mockEnvironmentVariableProvider.Setup(m => m.AwsAccessKeyId(It.IsAny<bool>())).Returns(AWS_ACCESS_KEY_ID);
        _mockEnvironmentVariableProvider.Setup(m => m.AwsSecretAccessKey(It.IsAny<bool>())).Returns(AWS_SECRET_ACCESS_KEY);
        _mockEnvironmentVariableProvider.Setup(m => m.AwsRegion(It.IsAny<bool>())).Returns(AWS_REGION);
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(false);
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateGitlabMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
        _mockGithubApi.Setup(x => x.StartGitlabMigration(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(MIGRATION_ID);
        _mockAwsApi.Setup(x => x.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>())).ReturnsAsync(ARCHIVE_URL);

        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
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

        _mockAwsApi.Verify(m => m.UploadToBucket(AWS_BUCKET_NAME, ARCHIVE_PATH, It.IsAny<string>()));
    }

    [Fact]
    public async Task Deletes_Archive_By_Default()
    {
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(false);
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateGitlabMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
        _mockGithubApi.Setup(x => x.StartGitlabMigration(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(MIGRATION_ID);
        _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
        using var archiveStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _mockFileSystemProvider.Setup(m => m.OpenRead(ARCHIVE_PATH)).Returns(archiveStream);

        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            GithubPat = GITHUB_PAT,
            QueueOnly = true,
        };

        await _handler.Handle(args);

        _mockFileSystemProvider.Verify(m => m.DeleteIfExists(ARCHIVE_PATH));
    }

    [Fact]
    public async Task Keeps_Archive_When_KeepArchive_Set()
    {
        _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(GITHUB_PAT);
        _mockGithubApi.Setup(x => x.DoesRepoExist(GITHUB_ORG, GITHUB_REPO)).ReturnsAsync(false);
        _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG)).ReturnsAsync(GITHUB_ORG_ID);
        _mockGithubApi.Setup(x => x.CreateGitlabMigrationSource(GITHUB_ORG_ID)).ReturnsAsync(MIGRATION_SOURCE_ID);
        _mockGithubApi.Setup(x => x.StartGitlabMigration(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(MIGRATION_ID);
        _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<Stream>())).ReturnsAsync(new Uri(ARCHIVE_URL));
        using var archiveStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _mockFileSystemProvider.Setup(m => m.OpenRead(ARCHIVE_PATH)).Returns(archiveStream);

        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            GithubPat = GITHUB_PAT,
            QueueOnly = true,
            KeepArchive = true,
        };

        await _handler.Handle(args);

        _mockFileSystemProvider.Verify(m => m.DeleteIfExists(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Throws_When_Gitlab_Pat_Not_Provided_For_Generate()
    {
        string nullGitlabPat = null;
        _mockEnvironmentVariableProvider.Setup(m => m.GitlabPat(It.IsAny<bool>())).Returns(nullGitlabPat);

        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabGroup = GITLAB_GROUP,
            GitlabProject = GITLAB_PROJECT,
        };

        await FluentActions
            .Invoking(() => _handler.Handle(args))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("*GitLab PAT*GITLAB_PAT*--gitlab-pat*");
    }

    [Fact]
    public async Task Throws_When_Archive_Path_Does_Not_Exist()
    {
        _mockFileSystemProvider.Setup(m => m.FileExists(ARCHIVE_PATH)).Returns(false);

        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
        };

        await FluentActions
            .Invoking(() => _handler.Handle(args))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage("*archive*--archive-path*");
    }
}
