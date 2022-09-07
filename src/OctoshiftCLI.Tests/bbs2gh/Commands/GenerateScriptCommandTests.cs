using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands;
using OctoshiftCLI.Contracts;
using Xunit;


namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class GenerateScriptCommandTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<IVersionProvider> _mockVersionProvider = new();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = new();

    private readonly GenerateScriptCommand _command;

    private const string GITHUB_ORG = "GITHUB-ORG";
    private const string BBS_SERVER_URL = "http://bbs-server-url";
    private const string BBS_USERNAME = "BBS-USERNAME";
    private const string SSH_USER = "SSH-USER";
    private const string SSH_PRIVATE_KEY = "/path-to-ssh-private-key";
    private const string SSH_PORT = "1234";
    private const string OUTPUT = "/path-to-output";

    public GenerateScriptCommandTests()
    {
        _command = new GenerateScriptCommand(_mockOctoLogger.Object, _mockVersionProvider.Object, _mockFileSystemProvider.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("generate-script");
        _command.Options.Count.Should().Be(8);

        TestHelpers.VerifyCommandOption(_command.Options, "bbs-server-url", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-username", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-private-key", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(_command.Options, "output", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }

    [Fact]
    public async Task Generated_Script_Contains_The_Migrate_Repo_Script_With_All_Options()
    {
        // Arrange
        const string migrateRepoCommand = $"Exec {{ gh bbs2gh migrate-repo --github-org \"{GITHUB_ORG}\" --bbs-server-url \"{BBS_SERVER_URL}\" --bbs-username \"{BBS_USERNAME}\" --ssh-user \"{SSH_USER}\" --ssh-private-key \"{SSH_PRIVATE_KEY}\" --ssh-port {SSH_PORT} --verbose --wait }}";

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            BbsUsername = BBS_USERNAME,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            SshPort = SSH_PORT,
            Output = new FileInfo(OUTPUT),
            Verbose = true
        };
        await _command.Invoke(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(OUTPUT, It.Is<string>(script => script.Contains(migrateRepoCommand))));
    }

    [Fact]
    public async Task Generated_Script_Contains_The_Cli_Version_Comment()
    {
        // Arrange
        _mockVersionProvider.Setup(m => m.GetCurrentVersion()).Returns("1.1.1.1");
        const string cliVersionComment = "# =========== Created with CLI version 1.1.1.1 ===========";

        // Act
        var args = new GenerateScriptCommandArgs()
        {
            BbsServerUrl = BBS_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            SshUser = SSH_USER,
            SshPrivateKey = SSH_PRIVATE_KEY,
            Output = new FileInfo(OUTPUT)
        };
        await _command.Invoke(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(OUTPUT, It.Is<string>(script => script.Contains(cliVersionComment))));
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
        await _command.Invoke(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(OUTPUT, It.Is<string>(script => script.StartsWith(shebang))));
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
        await _command.Invoke(args);

        // Assert
        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(OUTPUT, It.Is<string>(script => script.Contains(execFunctionBlock))));
    }
}
