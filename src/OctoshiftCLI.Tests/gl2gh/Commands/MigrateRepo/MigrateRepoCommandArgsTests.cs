using FluentAssertions;
using Moq;
using OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string ARCHIVE_PATH = "path/to/archive.tar";
    private const string ARCHIVE_URL = "https://archive-url/gitlab-archive.tar";
    private const string GITHUB_ORG = "target-org";
    private const string GITHUB_REPO = "target-repo";
    private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";
    private const string AWS_BUCKET_NAME = "aws-bucket-name";
    private const string AWS_ACCESS_KEY_ID = "aws-access-key-id";
    private const string AWS_SECRET_ACCESS_KEY = "aws-secret-access-key";
    private const string AWS_SESSION_TOKEN = "aws-session-token";
    private const string AWS_REGION = "aws-region";
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_GROUP = "gitlab-group";
    private const string GITLAB_PROJECT = "gitlab-project";
    private const string GITLAB_PAT = "gitlab-pat";

    [Fact]
    public void It_Throws_When_Neither_Gitlab_Server_Url_Nor_Archive_Source_Is_Provided()
    {
        var args = new MigrateRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--gitlab-server-url*--archive-path*--archive-url*");
    }

    [Fact]
    public void It_Throws_When_Both_Archive_Path_And_Archive_Url_Are_Provided()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            ArchiveUrl = ARCHIVE_URL,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--archive-path*--archive-url*");
    }

    [Fact]
    public void It_Throws_When_Gitlab_Group_Or_Project_Is_Missing_When_Generating_Archive()
    {
        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--gitlab-group*--gitlab-project*");
    }

    [Fact]
    public void It_Throws_When_Gitlab_Pat_Is_Provided_With_Archive_Path()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            GitlabPat = GITLAB_PAT
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--gitlab-pat*--archive-path*--archive-url*");
    }

    [Fact]
    public void It_Throws_When_No_Ssl_Verify_Is_Provided_With_Archive_Url()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchiveUrl = ARCHIVE_URL,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            NoSslVerify = true
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--no-ssl-verify*--archive-path*--archive-url*");
    }

    [Fact]
    public void It_Throws_When_Aws_Access_Key_Provided_Without_Bucket_Name()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            AwsAccessKey = AWS_ACCESS_KEY_ID
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*AWS S3*--aws-bucket-name*");
    }

    [Fact]
    public void It_Throws_When_Aws_Secret_Key_Provided_Without_Bucket_Name()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            AwsSecretKey = AWS_SECRET_ACCESS_KEY
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*AWS S3*--aws-bucket-name*");
    }

    [Fact]
    public void It_Throws_When_Aws_Session_Token_Provided_Without_Bucket_Name()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            AwsSessionToken = AWS_SESSION_TOKEN
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*AWS S3*--aws-bucket-name*");
    }

    [Fact]
    public void It_Throws_When_Aws_Region_Provided_Without_Bucket_Name()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            AwsRegion = AWS_REGION
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*AWS S3*--aws-bucket-name*");
    }

    [Fact]
    public void It_Throws_When_Use_Github_Storage_Provided_With_Aws_Bucket_Name()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AwsBucketName = AWS_BUCKET_NAME,
            UseGithubStorage = true
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--use-github-storage flag was provided with an AWS S3 Bucket name*");
    }

    [Fact]
    public void It_Throws_When_Use_Github_Storage_Provided_With_Azure_Storage_Connection_String()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchivePath = ARCHIVE_PATH,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING,
            UseGithubStorage = true
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--use-github-storage flag was provided with a connection string*");
    }

    [Fact]
    public void It_Throws_When_Github_Org_Is_Missing_For_Import()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchiveUrl = ARCHIVE_URL,
            GithubRepo = GITHUB_REPO
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--github-org*GitLab archive*");
    }

    [Fact]
    public void It_Throws_When_Github_Repo_Is_Missing_For_Import()
    {
        var args = new MigrateRepoCommandArgs
        {
            ArchiveUrl = ARCHIVE_URL,
            GithubOrg = GITHUB_ORG
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--github-repo*GitLab archive*");
    }

    [Fact]
    public void Valid_Generate_And_Upload_Args_Do_Not_Throw()
    {
        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabGroup = GITLAB_GROUP,
            GitlabProject = GITLAB_PROJECT,
            GitlabPat = GITLAB_PAT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
        };

        args.Invoking(x => x.Validate(_mockOctoLogger.Object)).Should().NotThrow();
    }
}
