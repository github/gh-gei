using System;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands.GenerateScript;
using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.GenerateScript;

public class GenerateScriptCommandTests
{
    private const string BBS_SERVER_URL = "http://bbs.contoso.com:7990";

    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<BbsApiFactory> _mockBbsApiFactory = TestHelpers.CreateMock<BbsApiFactory>();
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
        _mockServiceProvider.Setup(m => m.GetService(typeof(BbsApiFactory))).Returns(_mockBbsApiFactory.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        _command.Should().NotBeNull();
        _command.Name.Should().Be("generate-script");
        _command.Options.Count.Should().Be(21);

        TestHelpers.VerifyCommandOption(_command.Options, "bbs-server-url", true);
        TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-username", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-password", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-project", false);
        TestHelpers.VerifyCommandOption(_command.Options, "bbs-shared-home", false);
        TestHelpers.VerifyCommandOption(_command.Options, "archive-download-host", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-private-key", false);
        TestHelpers.VerifyCommandOption(_command.Options, "ssh-port", false);
        TestHelpers.VerifyCommandOption(_command.Options, "smb-user", false);
        TestHelpers.VerifyCommandOption(_command.Options, "smb-domain", false);
        TestHelpers.VerifyCommandOption(_command.Options, "output", false);
        TestHelpers.VerifyCommandOption(_command.Options, "kerberos", false, true);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(_command.Options, "aws-bucket-name", false);
        TestHelpers.VerifyCommandOption(_command.Options, "aws-region", false);
        TestHelpers.VerifyCommandOption(_command.Options, "keep-archive", false);
        TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "use-github-storage", false, true);
    }

    [Fact]
    public void It_Gets_A_Kerberos_HttpClient_When_Kerberos_Is_True()
    {
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            Kerberos = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.CreateKerberos(BBS_SERVER_URL, false));
    }

    [Fact]
    public void It_Gets_A_Kerberos_With_No_Ssl_Verify_HttpClient_When_Kerberos_And_No_Ssl_Verify_Are_True()
    {
        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            Kerberos = true,
            NoSslVerify = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.CreateKerberos(BBS_SERVER_URL, true));
    }

    [Fact]
    public void It_Gets_A_Default_HttpClient_When_Kerberos_And_No_Ssl_Verify_Are_Not_Set()
    {
        var bbsTestUser = "user";
        var bbsTestPassword = "password";

        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = bbsTestUser,
            BbsPassword = bbsTestPassword
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, bbsTestUser, bbsTestPassword, false));
    }

    [Fact]
    public void It_Gets_A_No_Ssl_Verify_HttpClient_When_No_Ssl_Verify_Is_Set()
    {
        var bbsTestUser = "user";
        var bbsTestPassword = "password";

        var args = new GenerateScriptCommandArgs
        {
            BbsServerUrl = BBS_SERVER_URL,
            BbsUsername = bbsTestUser,
            BbsPassword = bbsTestPassword,
            NoSslVerify = true
        };

        _command.BuildHandler(args, _mockServiceProvider.Object);

        _mockBbsApiFactory.Verify(m => m.Create(BBS_SERVER_URL, bbsTestUser, bbsTestPassword, true));
    }
}
