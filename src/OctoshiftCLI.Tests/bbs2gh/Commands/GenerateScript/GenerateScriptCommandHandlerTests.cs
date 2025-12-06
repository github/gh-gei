using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands.GenerateScript;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.GenerateScript;

public class GenerateScriptCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<IVersionProvider> _mockVersionProvider = new();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();

    private readonly GenerateScriptCommandHandler _handler;

    private const string GITHUB_ORG = "GITHUB-ORG";
    private const string BBS_SERVER_URL = "http://bbs-server-url";
    private const string BBS_USERNAME = "BBS-USERNAME";
    private const string BBS_PASSWORD = "BBS-PASSWORD";
    private const string SSH_USER = "SSH-USER";
    private const string SSH_PRIVATE_KEY = "path-to-ssh-private-key";
    private const string ARCHIVE_DOWNLOAD_HOST = "archive-download-host";
    private const int SSH_PORT = 2211;
    private const string SMB_USER = "SMB-USER";
    private const string SMB_DOMAIN = "SMB-DOMAIN";
    private const string OUTPUT = "unit-test-output";
    private const string BBS_FOO_PROJECT_KEY = "FP";
    private const string BBS_FOO_PROJECT_NAME = "BBS-FOO-PROJECT-NAME";
    private const string BBS_BAR_PROJECT_KEY = "BBS-BAR-PROJECT-NAME";
    private const string BBS_BAR_PROJECT_NAME = "BP";
    private const string BBS_FOO_REPO_1_SLUG = "foorepo1";
    private const string BBS_FOO_REPO_1_NAME = "BBS-FOO-REPO-1-NAME";
    private const string BBS_FOO_REPO_2_SLUG = "foorepo2";
    private const string BBS_FOO_REPO_2_NAME = "BBS-FOO-REPO-2-NAME";
    private const string BBS_BAR_REPO_1_SLUG = "barrepo1";
    private const string BBS_BAR_REPO_1_NAME = "BBS-BAR-REPO-1-NAME";
    private const string BBS_BAR_REPO_2_SLUG = "barrepo2";
    private const string BBS_BAR_REPO_2_NAME = "BBS-BAR-REPO-2-NAME";
    private const string BBS_SHARED_HOME = "BBS-SHARED-HOME";
    private const string AWS_BUCKET_NAME = "AWS-BUCKET-NAME";
    private const string AWS_REGION = "AWS_REGION";
    private const string UPLOADS_URL = "UPLOADS-URL";

    public GenerateScriptCommandHandlerTests()
    {
        _handler = new GenerateScriptCommandHandler(
            _mockOctoLogger.Object,
            _mockVersionProvider.Object,
            _mockFileSystemProvider.Object,
            _mockBbsApi.Object,
            _mockEnvironmentVariableProvider.Object);

        _mockEnvironmentVariableProvider.Setup(m => m.BbsUsername(It.IsAny<bool>())).Returns(BBS_USERNAME);
        _mockEnvironmentVariableProvider.Setup(m => m.BbsPassword(It.IsAny<bool>())).Returns(BBS_PASSWORD);
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[] { (1, BBS_FOO_PROJECT_KEY, BBS_FOO_PROJECT_NAME) });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[] { (1, BBS_FOO_REPO_1_SLUG, BBS_FOO_REPO_1_NAME) });
    }

    [Fact]
    public async Task No_Projects()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 33, 0) == "")));
    }

    [Fact]
    public async Task Validates_Env_Vars()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT),
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:GH_PAT) {
    Write-Error ""GH_PAT environment variable must be set to a valid GitHub Personal Access Token with the appropriate scopes. For more information see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer#creating-a-personal-access-token-for-github-enterprise-importer""
    exit 1
} else {
    Write-Host ""GH_PAT environment variable is set and will be used to authenticate to GitHub.""
}

if (-not $env:BBS_PASSWORD) {
    Write-Error ""BBS_PASSWORD environment variable must be set to a valid password that will be used to call Bitbucket Server/Data Center API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_PASSWORD environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs.""
}

if (-not $env:BBS_USERNAME) {
    Write-Error ""BBS_USERNAME environment variable must be set to a valid user that will be used to call Bitbucket Server/Data Center API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_USERNAME environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs.""
}

if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 9, 0) == TrimNonExecutableLines(expected, 0, 0))));
    }

    [Fact]
    public async Task Validates_Env_Vars_BBS_USERNAME_Not_Validated_When_Passed_As_Arg()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT),
            BbsUsername = BBS_USERNAME,
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:BBS_USERNAME) {
    Write-Error ""BBS_USERNAME environment variable must be set to a valid user that will be used to call BBS API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_USERNAME environment variable is set and will be used to authenticate to BBS APIs.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => !TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expected, 0, 0)))));
    }

    [Fact]
    public async Task Validates_Env_Vars_BBS_PASSWORD_Not_Validated_When_Kerberos()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT),
            Kerberos = true,
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:BBS_PASSWORD) {
    Write-Error ""BBS_PASSWORD environment variable must be set to a valid password that will be used to call BBS API's to generate a migration archive.""
    exit 1
} else {
    Write-Host ""BBS_PASSWORD environment variable is set and will be used to authenticate to BBS APIs.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => !TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expected, 0, 0)))));
    }

    [Fact]
    public async Task Validates_Env_Vars_AWS()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            AwsBucketName = AWS_BUCKET_NAME,
            Output = new FileInfo(OUTPUT),
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:AWS_ACCESS_KEY_ID) {
    Write-Error ""AWS_ACCESS_KEY_ID environment variable must be set to a valid AWS Access Key ID that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_ACCESS_KEY_ID environment variable is set and will be used to upload the migration archive to AWS S3.""
}
if (-not $env:AWS_SECRET_ACCESS_KEY) {
    Write-Error ""AWS_SECRET_ACCESS_KEY environment variable must be set to a valid AWS Secret Access Key that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_SECRET_ACCESS_KEY environment variable is set and will be used to upload the migration archive to AWS S3.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expected, 0, 0)))));
    }

    [Fact]
    public async Task Validates_Env_Vars_AZURE_STORAGE_CONNECTION_STRING_Not_Validated_When_Aws()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT),
            AwsBucketName = AWS_BUCKET_NAME,
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => !TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expected, 0, 0)))));
    }

    [Fact]
    public async Task Validates_Env_Vars_AZURE_STORAGE_CONNECTION_STRING_And_AWS_Not_Validated_When_UseGithubStorage()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT),
            UseGithubStorage = true
        };
        await _handler.Handle(args);

        var expectedAws = @"
if (-not $env:AWS_ACCESS_KEY_ID) {
    Write-Error ""AWS_ACCESS_KEY_ID environment variable must be set to a valid AWS Access Key ID that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_ACCESS_KEY_ID environment variable is set and will be used to upload the migration archive to AWS S3.""
}
if (-not $env:AWS_SECRET_ACCESS_KEY) {
    Write-Error ""AWS_SECRET_ACCESS_KEY environment variable must be set to a valid AWS Secret Access Key that will be used to upload the migration archive to AWS S3.""
    exit 1
} else {
    Write-Host ""AWS_SECRET_ACCESS_KEY environment variable is set and will be used to upload the migration archive to AWS S3.""
}";

        var expectedAzure = @"
if (-not $env:AZURE_STORAGE_CONNECTION_STRING) {
    Write-Error ""AZURE_STORAGE_CONNECTION_STRING environment variable must be set to a valid Azure Storage Connection String that will be used to upload the migration archive to Azure Blob Storage.""
    exit 1
} else {
    Write-Host ""AZURE_STORAGE_CONNECTION_STRING environment variable is set and will be used to upload the migration archive to Azure Blob Storage.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => !TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expectedAws, 0, 0)))));
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => !TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expectedAzure, 0, 0)))));
    }

    [Fact]
    public async Task Validates_Env_Vars_SMB_PASSWORD()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(Enumerable.Empty<(int Id, string Key, string Name)>());

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo(OUTPUT),
            SmbUser = SMB_USER,
        };
        await _handler.Handle(args);

        var expected = @"
if (-not $env:SMB_PASSWORD) {
    Write-Error ""SMB_PASSWORD environment variable must be set to a valid password that will be used to download the migration archive from your BBS server using SMB.""
    exit 1
} else {
    Write-Host ""SMB_PASSWORD environment variable is set and will be used to download the migration archive from your BBS server using SMB.""
}";

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 0, 0).Contains(TrimNonExecutableLines(expected, 0, 0)))));
    }

    [Fact]
    public async Task No_Repos()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(Enumerable.Empty<(int Id, string Slug, string Name)>());

        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 33, 0) == "")));
    }

    [Fact]
    public async Task Two_Projects_Two_Repos_Each_All_Options()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
            (Id: 2, Key: BBS_BAR_PROJECT_KEY, Name: BBS_BAR_PROJECT_NAME)
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
            (Id: 2, Slug: BBS_FOO_REPO_2_SLUG, Name: BBS_FOO_REPO_2_NAME)
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_BAR_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 3, Slug: BBS_BAR_REPO_1_SLUG, Name: BBS_BAR_REPO_1_NAME),
            (Id: 4, Slug: BBS_BAR_REPO_2_SLUG, Name: BBS_BAR_REPO_2_NAME)
        });

        var migrateRepoCommand1 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --keep-archive --target-repo-visibility private }}";
        var migrateRepoCommand2 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_2_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_2_SLUG}\" --verbose --keep-archive --target-repo-visibility private }}";
        var migrateRepoCommand3 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_BAR_PROJECT_KEY}\" --bbs-repo \"{BBS_BAR_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_BAR_PROJECT_KEY}-{BBS_BAR_REPO_1_SLUG}\" --verbose --keep-archive --target-repo-visibility private }}";
        var migrateRepoCommand4 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_BAR_PROJECT_KEY}\" --bbs-repo \"{BBS_BAR_REPO_2_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_BAR_PROJECT_KEY}-{BBS_BAR_REPO_2_SLUG}\" --verbose --keep-archive --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            KeepArchive = true
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand1))));
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand2))));
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand3))));
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand4))));

        _mockEnvironmentVariableProvider.Verify(m => m.BbsUsername(It.IsAny<bool>()), Times.Never);
        _mockEnvironmentVariableProvider.Verify(m => m.BbsPassword(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Filters_By_Project()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
            (Id: 2, Key: BBS_BAR_PROJECT_KEY, Name: BBS_BAR_PROJECT_NAME)
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
            (Id: 2, Slug: BBS_FOO_REPO_2_SLUG, Name: BBS_FOO_REPO_2_NAME)
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_BAR_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 3, Slug: BBS_BAR_REPO_1_SLUG, Name: BBS_BAR_REPO_1_NAME),
            (Id: 4, Slug: BBS_BAR_REPO_2_SLUG, Name: BBS_BAR_REPO_2_NAME)
        });

        var migrateRepoCommand1 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --target-repo-visibility private }}";
        var migrateRepoCommand2 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_2_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_2_SLUG}\" --verbose --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsProject = BBS_FOO_PROJECT_KEY,
            BbsSharedHome = BBS_SHARED_HOME,
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand1))));
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand2))));

        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(BBS_BAR_PROJECT_KEY))), Times.Never);
    }

    [Fact]
    public async Task One_Repo_With_Kerberos()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });

        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --kerberos --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            Kerberos = true,
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task One_Repo_With_No_Ssl_Verify()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });

        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --no-ssl-verify --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            NoSslVerify = true,
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task One_Repo_With_Smb()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });

        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --smb-user \"{SMB_USER}\" --smb-domain {SMB_DOMAIN} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            SmbUser = SMB_USER,
            SmbDomain = SMB_DOMAIN,
            Output = new FileInfo(OUTPUT),
            Verbose = true
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task One_Repo_With_Smb_And_TargetApiUrl()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });
        var targetApiUrl = "https://foo.com/api/v3";
        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --target-api-url \"{targetApiUrl}\" --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --smb-user \"{SMB_USER}\" --smb-domain {SMB_DOMAIN} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            SmbUser = SMB_USER,
            SmbDomain = SMB_DOMAIN,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            TargetApiUrl = targetApiUrl
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task One_Repo_With_Smb_And_Archive_Download_Host()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });

        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --smb-user \"{SMB_USER}\" --smb-domain {SMB_DOMAIN} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            SmbUser = SMB_USER,
            SmbDomain = SMB_DOMAIN,
            Output = new FileInfo(OUTPUT),
            Verbose = true
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task Generated_Script_Contains_The_Cli_Version_Comment()
    {
        // Arrange
        _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1");
        const string cliVersionComment = "# =========== Created with CLI version 1.1.1 ===========";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(cliVersionComment))));
    }

    [Fact]
    public async Task Generated_Script_StartsWith_Shebang()
    {
        // Arrange
        const string shebang = "#!/usr/bin/env pwsh";

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.StartsWith(shebang))));
    }

    [Fact]
    public async Task Generated_Script_Contains_Exec_Function_Block()
    {
        // Arrange
        const string execFunctionBlock = @"
function Exec {
    param (
        [scriptblock]$ScriptBlock
    )
    & @ScriptBlock
    if ($lastexitcode -ne 0) {
        exit $lastexitcode
    }
}";

        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(execFunctionBlock))));
    }

    [Fact]
    public async Task One_Repo_With_Aws_Bucket_Name_And_Region()
    {
        // Arrange
        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_FOO_PROJECT_KEY, Name: BBS_FOO_PROJECT_NAME),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_FOO_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_FOO_REPO_1_SLUG, Name: BBS_FOO_REPO_1_NAME),
        });

        var migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" " +
                                 $"--bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" " +
                                 $"--ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --archive-download-host {ARCHIVE_DOWNLOAD_HOST} --github-org \"{GITHUB_ORG}\" " +
                                 $"--github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --aws-bucket-name \"{AWS_BUCKET_NAME}\" " +
                                 $"--aws-region \"{AWS_REGION}\" --target-repo-visibility private }}";

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            BbsPassword = BBS_PASSWORD,
            BbsSharedHome = BBS_SHARED_HOME,
            ArchiveDownloadHost = ARCHIVE_DOWNLOAD_HOST,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            AwsBucketName = AWS_BUCKET_NAME,
            AwsRegion = AWS_REGION
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task BBS_Single_Repo_With_UseGithubStorage()
    {
        // Arrange
        var TARGET_API_URL = "https://foo.com/api/v3";
        const string BBS_PROJECT_KEY = "BBS-PROJECT";
        const string BBS_REPO_SLUG = "repo-slug";

        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_PROJECT_KEY, Name: "BBS Project Name"),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_REPO_SLUG, Name: "RepoName"),
         });


        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo("unit-test-output"),
            UseGithubStorage = true,
            TargetApiUrl = TARGET_API_URL,
            BbsProject = BBS_PROJECT_KEY,
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script =>
            script.Contains("--bbs-server-url \"http://bbs-server-url\"") &&
            script.Contains("--bbs-project \"BBS-PROJECT\"") &&
            script.Contains("--github-org \"GITHUB-ORG\"") &&
            script.Contains("--use-github-storage")
)));

    }
    [Fact]
    public async Task BBS_Single_Repo_With_TargetUploadsUrl()
    {
        // Arrange
        var TARGET_API_URL = "https://foo.com/api/v3";
        const string BBS_PROJECT_KEY = "BBS-PROJECT";
        const string BBS_REPO_SLUG = "repo-slug";

        _mockBbsApi.Setup(m => m.GetProjects()).ReturnsAsync(new[]
        {
            (Id: 1, Key: BBS_PROJECT_KEY, Name: "BBS Project Name"),
        });
        _mockBbsApi.Setup(m => m.GetRepos(BBS_PROJECT_KEY)).ReturnsAsync(new[]
        {
            (Id: 1, Slug: BBS_REPO_SLUG, Name: "RepoName"),
        });

        // Act
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo("unit-test-output"),
            TargetApiUrl = TARGET_API_URL,
            TargetUploadsUrl = UPLOADS_URL,
            BbsProject = BBS_PROJECT_KEY,
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script =>
            script.Contains("--bbs-server-url \"http://bbs-server-url\"") &&
            script.Contains("--bbs-project \"BBS-PROJECT\"") &&
            script.Contains("--github-org \"GITHUB-ORG\"") &&
            script.Contains("--target-uploads-url \"UPLOADS-URL\"")
        )));
    }

    private string TrimNonExecutableLines(string script, int skipFirst = 9, int skipLast = 0)
    {
        var lines = script.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

        lines = lines
            .Where(x => x.HasValue())
            .Where(x => !x.Trim().StartsWith("#"))
            .Skip(skipFirst)
            .SkipLast(skipLast);

        var result = string.Join(Environment.NewLine, lines);
        return result;
    }
}
