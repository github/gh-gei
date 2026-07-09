using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GitlabToGithub.Commands.GenerateScript;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.GenerateScript;

public class GenerateScriptCommandTests
{
    private const string GITLAB_SERVER_URL = "https://gitlab.contoso.com";
    private const string GITLAB_PAT = "gitlab-pat";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
    private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
    private readonly Mock<IVersionProvider> _mockVersionProvider = new();

    private readonly GenerateScriptCommand _command = [];

    public GenerateScriptCommandTests()
    {
        _mockServiceProvider.Setup(m => m.GetService(typeof(OctoLogger))).Returns(_mockOctoLogger.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(EnvironmentVariableProvider))).Returns(_mockEnvironmentVariableProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(FileSystemProvider))).Returns(_mockFileSystemProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(IVersionProvider))).Returns(_mockVersionProvider.Object);
        _mockServiceProvider.Setup(m => m.GetService(typeof(GitlabApiFactory))).Returns(_mockGitlabApiFactory.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("generate-script");
        _command.Options.Count.Should().Be(14);

        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-server-url", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "target-uploads-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-group", false);
        TestHelpers.VerifyCommandOption(_command.Options, "gitlab-project", false);
        TestHelpers.VerifyCommandOption(_command.Options, "output", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(_command.Options, "aws-bucket-name", false);
        TestHelpers.VerifyCommandOption(_command.Options, "aws-region", false);
        TestHelpers.VerifyCommandOption(_command.Options, "keep-archive", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "use-github-storage", false);
    }

    [Fact]
    public void It_Creates_The_GitlabApi_With_The_Provided_Server_Url_And_Pat()
    {
        var args = new GenerateScriptCommandArgs
        {
            GitlabServerUrl = GITLAB_SERVER_URL,
            GitlabPat = GITLAB_PAT
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockGitlabApiFactory.Verify(m => m.Create(GITLAB_SERVER_URL, GITLAB_PAT, false));
    }
}
