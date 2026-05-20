using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommandHandlerTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<IVersionProvider> _mockVersionProvider = new();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();

    private readonly GenerateScriptCommandHandler _handler;

    private const string GITHUB_ORG = "GITHUB-ORG";
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string OUTPUT = "unit-test-output";
    private const string GROUP_PATH_FOO = "group-foo";
    private const string GROUP_NAME_FOO = "Group Foo";
    private const string GROUP_PATH_BAR = "group-bar";
    private const string GROUP_NAME_BAR = "Group Bar";
    private const string PROJECT_PATH_1 = "project-1";
    private const string PROJECT_NAME_1 = "Project 1";
    private const string PROJECT_PATH_2 = "project-2";
    private const string PROJECT_NAME_2 = "Project 2";
    private const string AWS_BUCKET_NAME = "AWS-BUCKET-NAME";
    private const string AWS_REGION = "us-east-1";

    public GenerateScriptCommandHandlerTests()
    {
        _handler = new GenerateScriptCommandHandler(
            _mockOctoLogger.Object,
            _mockVersionProvider.Object,
            _mockFileSystemProvider.Object,
            _mockGitlabApi.Object,
            _mockEnvironmentVariableProvider.Object);
    }

    [Fact]
    public async Task No_Output_Path_Does_Not_Write_File()
    {
        _mockGitlabApi.Setup(m => m.GetGroups()).ReturnsAsync(System.Array.Empty<(long Id, string Path, string Name)>());

        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GithubOrg = GITHUB_ORG
        };

        await _handler.Handle(args);

        _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task No_Groups_Generates_Header_Only()
    {
        _mockGitlabApi.Setup(m => m.GetGroups()).ReturnsAsync(System.Array.Empty<(long Id, string Path, string Name)>());

        string capturedScript = null;
        _mockFileSystemProvider
            .Setup(m => m.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contents) => capturedScript = contents)
            .Returns(Task.CompletedTask);

        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo(OUTPUT)
        };

        await _handler.Handle(args);

        capturedScript.Should().NotBeNullOrEmpty();
        capturedScript.Should().Contain("VALIDATE_GH_PAT".Replace("VALIDATE_GH_PAT", "GH_PAT"));
        capturedScript.Should().Contain("GITLAB_PAT");
        capturedScript.Should().NotContain("# =========== Group:");
    }

    [Fact]
    public async Task Default_Generates_Migrate_Repo_Command_For_Each_Project()
    {
        _mockGitlabApi
            .Setup(m => m.GetGroups())
            .ReturnsAsync(new[]
            {
                (Id: 1L, Path: GROUP_PATH_FOO, Name: GROUP_NAME_FOO),
                (Id: 2L, Path: GROUP_PATH_BAR, Name: GROUP_NAME_BAR)
            });
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_FOO))
            .ReturnsAsync(new[]
            {
                (Id: 1L, Path: PROJECT_PATH_1, Name: PROJECT_NAME_1, Archived: false),
                (Id: 2L, Path: PROJECT_PATH_2, Name: PROJECT_NAME_2, Archived: false)
            });
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_BAR))
            .ReturnsAsync(System.Array.Empty<(long Id, string Path, string Name, bool Archived)>());

        string capturedScript = null;
        _mockFileSystemProvider
            .Setup(m => m.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contents) => capturedScript = contents)
            .Returns(Task.CompletedTask);

        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo(OUTPUT)
        };

        await _handler.Handle(args);

        capturedScript.Should().NotBeNullOrEmpty();
        capturedScript.Should().Contain($"# =========== Group: {GROUP_PATH_FOO} ===========");
        capturedScript.Should().Contain($"# =========== Group: {GROUP_PATH_BAR} ===========");
        capturedScript.Should().Contain($"--gitlab-server-url \"{GITLAB_SERVER_URL}\"");
        capturedScript.Should().Contain($"--gitlab-group \"{GROUP_PATH_FOO}\"");
        capturedScript.Should().Contain($"--gitlab-project \"{PROJECT_PATH_1}\"");
        capturedScript.Should().Contain($"--gitlab-project \"{PROJECT_PATH_2}\"");
        capturedScript.Should().Contain($"--github-org \"{GITHUB_ORG}\"");
        capturedScript.Should().Contain($"--github-repo \"{GROUP_PATH_FOO}-{PROJECT_PATH_1}\"");
        capturedScript.Should().Contain("--target-repo-visibility private");
        capturedScript.Should().Contain("Skipping this group because it has no projects.");
        capturedScript.Should().NotContain("--queue-only");
        capturedScript.Should().NotContain("--verbose");
        capturedScript.Should().NotContain("--aws-bucket-name");
        capturedScript.Should().NotContain("--aws-region");
        capturedScript.Should().NotContain("--keep-archive");
        capturedScript.Should().NotContain("--use-github-storage");
        capturedScript.Should().NotContain("--no-ssl-verify");
        capturedScript.Should().NotContain("--kerberos");
    }

    [Fact]
    public async Task Includes_Optional_Flags_When_Set()
    {
        _mockGitlabApi
            .Setup(m => m.GetGroups())
            .ReturnsAsync(new[] { (Id: 1L, Path: GROUP_PATH_FOO, Name: GROUP_NAME_FOO) });
        _mockGitlabApi
            .Setup(m => m.GetProjects(GROUP_PATH_FOO))
            .ReturnsAsync(new[] { (Id: 1L, Path: PROJECT_PATH_1, Name: PROJECT_NAME_1, Archived: false) });

        string capturedScript = null;
        _mockFileSystemProvider
            .Setup(m => m.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contents) => capturedScript = contents)
            .Returns(Task.CompletedTask);

        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo(OUTPUT),
            Verbose = true,
            AwsBucketName = AWS_BUCKET_NAME,
            AwsRegion = AWS_REGION,
            KeepArchive = true
        };

        await _handler.Handle(args);

        capturedScript.Should().Contain("--verbose");
        capturedScript.Should().Contain($"--aws-bucket-name \"{AWS_BUCKET_NAME}\"");
        capturedScript.Should().Contain($"--aws-region \"{AWS_REGION}\"");
        capturedScript.Should().Contain("--keep-archive");
        capturedScript.Should().Contain("VALIDATE_AWS_ACCESS_KEY_ID".Replace("VALIDATE_", "").Replace("ID", "ID"));
        capturedScript.Should().Contain("AWS_SECRET_ACCESS_KEY");
        capturedScript.Should().NotContain("AZURE_STORAGE_CONNECTION_STRING");
    }

    [Fact]
    public async Task UseGithubStorage_Skips_Azure_And_Aws_Validation()
    {
        _mockGitlabApi.Setup(m => m.GetGroups()).ReturnsAsync(System.Array.Empty<(long Id, string Path, string Name)>());

        string capturedScript = null;
        _mockFileSystemProvider
            .Setup(m => m.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, contents) => capturedScript = contents)
            .Returns(Task.CompletedTask);

        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GithubOrg = GITHUB_ORG,
            Output = new FileInfo(OUTPUT),
            UseGithubStorage = true
        };

        await _handler.Handle(args);

        capturedScript.Should().NotContain("AZURE_STORAGE_CONNECTION_STRING");
        capturedScript.Should().NotContain("AWS_ACCESS_KEY_ID");
    }
}
