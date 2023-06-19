using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub.Commands.GenerateScript;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.GenerateScript;

public class GenerateScriptCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly GenerateScriptCommandArgs _args = new();

    [Fact]
    public void It_Throws_If_BbsServer_Url_Is_Not_Provided_But_No_Ssl_Verify_Is_Provided()
    {
        // Act
        _args.NoSslVerify = true;
        _args.BbsServerUrl = "";

        // Assert
        _args.Invoking(x => x.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("*--no-ssl-verify*--bbs-server-url*");
    }

    [Fact]
    public void Invoke_With_Ssh_Port_Set_To_7999_Logs_Warning()
    {
        _args.SshPort = 7999;

        _args.Validate(_mockOctoLogger.Object);

        _mockOctoLogger.Verify(x => x.LogWarning(It.Is<string>(x => x.ToLower().Contains("--ssh-port is set to 7999"))));
    }
}
