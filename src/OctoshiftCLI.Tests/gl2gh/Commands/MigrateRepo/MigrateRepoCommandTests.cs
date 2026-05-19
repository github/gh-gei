using System;
using System.Net.Http;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Factories;
using OctoshiftCLI.GitlabToGithub.Commands.MigrateRepo;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.MigrateRepo;

public class MigrateRepoCommandTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_PAT = "gitlab-pat";
    private const string GITHUB_ORG = "github-org";
    private const string GITHUB_PAT = "github-pat";
    private const string AZURE_STORAGE_CONNECTION_STRING = "azure-storage-connection-string";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
    private readonly Mock<IAzureApiFactory> _mockAzureApiFactory = new();
    private readonly Mock<WarningsCountLogger> _warningsCountLogger = TestHelpers.CreateMock<WarningsCountLogger>();

    private readonly MigrateRepoCommand _command = [];

    public MigrateRepoCommandTests()
    {
        _mockServiceProvider.Setup(m => m.GetService(typeof(OctoLogger))).Returns(_mockOctoLogger.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(EnvironmentVariableProvider))).Returns(_mockEnvironmentVariableProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(FileSystemProvider))).Returns(_mockFileSystemProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(ITargetGithubApiFactory))).Returns(_mockGithubApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(GitlabApiFactory))).Returns(_mockGitlabApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(IAzureApiFactory))).Returns(_mockAzureApiFactory.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(HttpDownloadServiceFactory)))
            .Returns(new HttpDownloadServiceFactory(
                _mockOctoLogger.Object,
                new Mock<IHttpClientFactory>().Object,
                _mockFileSystemProvider.Object,
                new Mock<IVersionProvider>().Object));
        _mockServiceProvider.Setup(m => m.GetService(typeof(WarningsCountLogger))).Returns(_warningsCountLogger.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        var command = new MigrateRepoCommand();
        command.Should().NotBeNull();
        command.Name.Should().Be("migrate-repo");
        command.Options.Count.Should().Be(23);

        TestHelpers.VerifyCommandOption(command.Options, "gitlab-server-url", true);
        TestHelpers.VerifyCommandOption(command.Options, "gitlab-group", true);
        TestHelpers.VerifyCommandOption(command.Options, "gitlab-project", true);
        TestHelpers.VerifyCommandOption(command.Options, "gitlab-pat", false);
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
        TestHelpers.VerifyCommandOption(command.Options, "queue-only", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-repo-visibility", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "keep-archive", false);
        TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-uploads-url", false, true);
        TestHelpers.VerifyCommandOption(command.Options, "use-github-storage", false, true);
    }

    [Fact]
    public void BuildHandler_Creates_The_Handler()
    {
        var args = new MigrateRepoCommandArgs();

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockGitlabApiFactory.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _mockAzureApiFactory.Verify(m => m.Create(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildHandler_Creates_GitHub_Api_When_Github_Org_Is_Provided()
    {
        var args = new MigrateRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubPat = GITHUB_PAT
        };

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockGithubApiFactory.Verify(m => m.Create(null, null, GITHUB_PAT));
    }

    [Fact]
    public void BuildHandler_Uses_Target_Api_Url_When_Provided()
    {
        var targetApiUrl = "https://api.github.com";
        var args = new MigrateRepoCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubPat = GITHUB_PAT,
            TargetApiUrl = targetApiUrl
        };

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null, GITHUB_PAT));
    }

    [Fact]
    public void BuildHandler_Creates_Gitlab_Api_When_Gitlab_Server_Url_Is_Provided()
    {
        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT
        };

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockGitlabApiFactory.Verify(m => m.Create(GITLAB_SERVER_URL, GITLAB_PAT, false));
    }

    [Fact]
    public void BuildHandler_Forwards_NoSslVerify_To_Gitlab_Api_Factory()
    {
        var args = new MigrateRepoCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT,
            NoSslVerify = true
        };

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockGitlabApiFactory.Verify(m => m.Create(GITLAB_SERVER_URL, GITLAB_PAT, true));
    }

    [Fact]
    public void BuildHandler_Creates_Azure_Api_When_Connection_String_Is_Provided_Via_Args()
    {
        var args = new MigrateRepoCommandArgs
        {
            AzureStorageConnectionString = AZURE_STORAGE_CONNECTION_STRING
        };

        var handler = _command.BuildHandler(args, _mockServiceProvider.Object);

        handler.Should().NotBeNull();
        _mockAzureApiFactory.Verify(m => m.Create(AZURE_STORAGE_CONNECTION_STRING));
    }
}
