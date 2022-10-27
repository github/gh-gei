using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Commands;
using OctoshiftCLI.BbsToGithub.Handlers;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using Xunit;


namespace OctoshiftCLI.Tests.bbs2gh.Handlers;

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
    private const string SSH_PORT = "1234";
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
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 9, 0) == "")));
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
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => TrimNonExecutableLines(script, 9, 0) == "")));
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

        const string migrateRepoCommand1 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --wait }}";
        const string migrateRepoCommand2 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_2_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_2_SLUG}\" --verbose --wait }}";
        const string migrateRepoCommand3 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_BAR_PROJECT_KEY}\" --bbs-repo \"{BBS_BAR_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_BAR_PROJECT_KEY}-{BBS_BAR_REPO_1_SLUG}\" --verbose --wait }}";
        const string migrateRepoCommand4 = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_BAR_PROJECT_KEY}\" --bbs-repo \"{BBS_BAR_REPO_2_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_BAR_PROJECT_KEY}-{BBS_BAR_REPO_2_SLUG}\" --verbose --wait }}";

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
            Verbose = true
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

        const string migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --wait --kerberos }}";

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
            Kerberos = true,
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task Generated_Script_Contains_The_Cli_Version_Comment()
    {
        // Arrange
        _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");
        const string cliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

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
    public async Task One_Repo_With_Aws_Bucket_Name()
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

        const string migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --bbs-shared-home \"{BBS_SHARED_HOME}\" --bbs-project \"{BBS_FOO_PROJECT_KEY}\" --bbs-repo \"{BBS_FOO_REPO_1_SLUG}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --github-org \"{GITHUB_ORG}\" --github-repo \"{BBS_FOO_PROJECT_KEY}-{BBS_FOO_REPO_1_SLUG}\" --verbose --wait --aws-bucket-name \"{AWS_BUCKET_NAME}\" }}";

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
            AwsBucketName = AWS_BUCKET_NAME
        };
        await _handler.Handle(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    private string TrimNonExecutableLines(string script, int skipFirst = 9, int skipLast = 0)
    {
        var lines = script.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable();

        lines = lines
            .Where(x => x.HasValue())
            .Where(x => !x.Trim().StartsWith("#"))
            .Skip(skipFirst)
            .SkipLast(skipLast);

        return string.Join(Environment.NewLine, lines);
    }
}
