using System;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class OctoLoggerTests
{
    private string _logOutput;
    private string _verboseLogOutput;
    private string _consoleOutput;
    private string _consoleError;

    private readonly OctoLogger _octoLogger;

    public OctoLoggerTests()
    {
        _octoLogger = new OctoLogger(CaptureLogOutput, CaptureVerboseLogOutput, CaptureConsoleOutput, CaptureConsoleError);
    }

    [Fact]
    public void Secrets_Should_Be_Masked_From_Logs_And_Console()
    {
        var secret = "purplemonkeydishwasher";
        var urlEncodedSecret = Uri.EscapeDataString(secret);

        _octoLogger.RegisterSecret(secret);

        _octoLogger.Verbose = false;
        _octoLogger.LogInformation($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogInformation($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");
        _octoLogger.LogVerbose($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogVerbose($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");
        _octoLogger.LogWarning($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogWarning($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");
        _octoLogger.LogSuccess($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogSuccess($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");
        _octoLogger.LogError($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogError($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");
        _octoLogger.LogError(new OctoshiftCliException($"Don't tell anybody that {secret} is my password"));
        _octoLogger.LogError(new OctoshiftCliException($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password"));
        _octoLogger.LogError(new InvalidOperationException($"Don't tell anybody that {secret} is my password"));
        _octoLogger.LogError(new InvalidOperationException($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password"));

        _octoLogger.Verbose = true;
        _octoLogger.LogVerbose($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogVerbose($"Don't tell anyone that {urlEncodedSecret} is my URL encoded password");

        _consoleOutput.Should().NotContain(secret);
        _logOutput.Should().NotContain(secret);
        _verboseLogOutput.Should().NotContain(secret);
        _consoleError.Should().NotContain(secret);

        _consoleOutput.Should().NotContain(urlEncodedSecret);
        _logOutput.Should().NotContain(urlEncodedSecret);
        _verboseLogOutput.Should().NotContain(urlEncodedSecret);
        _consoleError.Should().NotContain(urlEncodedSecret);
    }

    [Theory]
    [InlineData("https://files.github.acmeinc.com/foo?token=foobar")]
    [InlineData("HTTPS://FILES.GITHUB.ACMEINC.COM/FOO?TOKEN=FOOBAR")]
    public void Ghes_Archive_Url_Tokens_Should_Be_Replaced_In_Logs_And_Console(string archiveUrl)
    {
        _octoLogger.Verbose = false;
        _octoLogger.LogInformation($"Archive URL: {archiveUrl}");
        _octoLogger.LogVerbose($"Archive URL: {archiveUrl}");
        _octoLogger.LogWarning($"Archive URL: {archiveUrl}");
        _octoLogger.LogSuccess($"Archive URL: {archiveUrl}");
        _octoLogger.LogError($"Archive URL: {archiveUrl}");
        _octoLogger.LogError(new OctoshiftCliException($"Archive URL: {archiveUrl}"));
        _octoLogger.LogError(new InvalidOperationException($"Archive URL: {archiveUrl}"));

        _octoLogger.Verbose = true;
        _octoLogger.LogVerbose($"Archive URL: {archiveUrl}");

        _consoleOutput.Should().NotContain(archiveUrl);
        _logOutput.Should().NotContain(archiveUrl);
        _verboseLogOutput.Should().NotContain(archiveUrl);
        _consoleError.Should().NotContain(archiveUrl);

        _consoleOutput.ToLower().Should().Contain("?token=***");
    }

    [Theory]
    [InlineData("https://example-s3-bucket-name.s3.amazonaws.com/uuid-uuid-uuid.tar.gz?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AAAAAAAAAAAAAAAAAAAAAAA&X-Amz-Date=20231025T104425Z&X-Amz-Expires=172800&X-Amz-Signature=AAAAAAAAAAAAAAAAAAAAAAA&X-Amz-SignedHeaders=host&actor_id=1&key_id=0&repo_id=0&response-content-disposition=filename%3Duuid-uuid-uuid.tar.gz&response-content-type=application%2Fx-gzip")]
    [InlineData("HTTPS://EXAMPLE-S3-BUCKET-NAME.S3.AMAZONAWS.COM/UUID-UUID-UUID.TAR.GZ?X-AMZ-ALGORITHM=AWS4-HMAC-SHA256&X-AMZ-CREDENTIAL=AAAAAAAAAAAAAAAAAAAAAAA&X-AMZ-DATE=20231025T104425Z&X-AMZ-EXPIRES=172800&X-AMZ-SIGNATURE=AAAAAAAAAAAAAAAAAAAAAAA&X-AMZ-SIGNEDHEADERS=HOST&ACTOR_ID=1&KEY_ID=0&REPO_ID=0&RESPONSE-CONTENT-DISPOSITION=FILENAME%3DUUID-UUID-UUID.TAR.GZ&RESPONSE-CONTENT-TYPE=APPLICATION%2FX-GZIP")]
    public void Aws_Url_X_Aws_Credential_Parameters_Should_Be_Replaced_In_Logs_And_Console(string awsUrl)
    {
        _octoLogger.Verbose = false;
        _octoLogger.LogInformation($"Archive (metadata) download url: {awsUrl}");
        _octoLogger.LogVerbose($"Archive (metadata) download url: {awsUrl}");
        _octoLogger.LogWarning($"Archive (metadata) download url: {awsUrl}");
        _octoLogger.LogSuccess($"Archive (metadata) download url: {awsUrl}");
        _octoLogger.LogError($"Archive (metadata) download url: {awsUrl}");
        _octoLogger.LogError(new OctoshiftCliException($"Archive (metadata) download url: {awsUrl}"));
        _octoLogger.LogError(new InvalidOperationException($"Archive (metadata) download url: {awsUrl}"));

        _octoLogger.Verbose = true;
        _octoLogger.LogVerbose($"Archive (metadata) download url: {awsUrl}");

        _consoleOutput.Should().NotContain(awsUrl);
        _logOutput.Should().NotContain(awsUrl);
        _verboseLogOutput.Should().NotContain(awsUrl);
        _consoleError.Should().NotContain(awsUrl);

        _consoleOutput.ToLower().Should().Contain("&x-amz-credential=***");
    }

    [Theory]
    [InlineData("https://t3a00c49dev02arg01sa01.blob.core.windows.net/migration-archives-a9fd67c9-e987-4b3e-9cf2-439a95b7f275/f4e871a0-3214-4f94-a82b-8937cece6234.tar?sv=2023-11-03&se=2025-04-10T22%3A32%3A24Z&sr=b&sp=r&sig=y1rXBOGONXMXup%2B0%3D")]
    [InlineData("https://t3a00c49dev02arg01sa01.blob.core.windows.net/migration-archives-a9fd67c9-e987-4b3e-9cf2-439a95b7f275/f4e871a0-3214-4f94-a82b-8937cece6234.tar?sig=y1rXBOGONXMXup%2B0%3D&sv=2023-11-03&se=2025-04-10T22%3A32%3A24Z&sr=b&sp=r")]
    public void Azure_SAS_URL_Sig_Parameter_Should_Be_Redacted_In_Logs_And_Console(string sasUrl)
    {
        const string sig = "y1rXBOGONXMXup%2B0%3D";
        // Belt and suspenders
        sasUrl.Should().Contain(sig);

        _octoLogger.Verbose = false;
        _octoLogger.LogInformation($"Archive (metadata) download url: {sasUrl}");
        _octoLogger.LogVerbose($"Archive (metadata) download url: {sasUrl}");
        _octoLogger.LogWarning($"Archive (metadata) download url: {sasUrl}");
        _octoLogger.LogSuccess($"Archive (metadata) download url: {sasUrl}");
        _octoLogger.LogError($"Archive (metadata) download url: {sasUrl}");
        _octoLogger.LogError(new OctoshiftCliException($"Archive (metadata) download url: {sasUrl}"));
        _octoLogger.LogError(new InvalidOperationException($"Archive (metadata) download url: {sasUrl}"));

        _octoLogger.Verbose = true;
        _octoLogger.LogVerbose($"Archive (metadata) download url: {sasUrl}");

        _consoleOutput.Should().NotContain(sasUrl);
        _logOutput.Should().NotContain(sasUrl);
        _verboseLogOutput.Should().NotContain(sasUrl);
        _consoleError.Should().NotContain(sasUrl);

        _consoleOutput.ToLower().Should().Contain("sig=***");
    }

    [Fact]
    public void LogError_For_OctoshiftCliException_Should_Log_Exception_Message_In_Non_Verbose_Mode()
    {
        // Arrange
        const string userFriendlyMessage = "A user friendly message";
        const string exceptionDetails = "exception details";
        var octoshiftCliException = new OctoshiftCliException(userFriendlyMessage,
            new ArgumentNullException("arg", exceptionDetails));

        // Act
        _octoLogger.LogError(octoshiftCliException);

        // Assert
        _consoleOutput.Should().BeNull();

        _consoleError.Trim().Should().EndWith($"[ERROR] {userFriendlyMessage}");
        _consoleError.Should().NotContain(exceptionDetails);

        _logOutput.Trim().Should().EndWith($"[ERROR] {userFriendlyMessage}");
        _logOutput.Should().NotContain(exceptionDetails);

        _verboseLogOutput.Trim().Should().EndWith($"[ERROR] {octoshiftCliException}");
    }

    [Fact]
    public void LogError_For_Unexpected_Exception_Should_Log_Generic_Error_Message_In_Non_Verbose_Mode()
    {
        // Arrange
        const string genericErrorMessage = "An unexpected error happened. Please see the logs for details.";
        const string userEnemyMessage = "Some user enemy error message!";
        const string exceptionDetails = "exception details";
        var unexpectedException = new InvalidOperationException(userEnemyMessage,
            new ArgumentNullException("arg", exceptionDetails));

        // Act
        _octoLogger.LogError(unexpectedException);

        // Assert
        _consoleOutput.Should().BeNull();

        _consoleError.Trim().Should().EndWith($"[ERROR] {genericErrorMessage}");
        _consoleError.Should().NotContain(userEnemyMessage);
        _consoleError.Should().NotContain(exceptionDetails);

        _logOutput.Trim().Should().EndWith($"[ERROR] {genericErrorMessage}");
        _logOutput.Should().NotContain(userEnemyMessage);
        _logOutput.Should().NotContain(exceptionDetails);

        _verboseLogOutput.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
        _verboseLogOutput.Should().NotContain(genericErrorMessage);
    }

    [Fact]
    public void LogError_For_Any_Exception_Should_Always_Log_Entire_Exception_In_Verbose_Mode()
    {
        // Arrange
        const string genericErrorMessage = "An unexpected error happened. Please see the logs for details.";

        _octoLogger.Verbose = true;

        var unexpectedException =
            new InvalidOperationException("Some user enemy error message!", new ArgumentNullException("arg"));

        // Act
        _octoLogger.LogError(unexpectedException);

        // Assert
        _consoleOutput.Should().BeNull();

        _consoleError.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
        _consoleError.Should().NotContain(genericErrorMessage);

        _logOutput.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
        _logOutput.Should().NotContain(genericErrorMessage);

        _verboseLogOutput.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
        _verboseLogOutput.Should().NotContain(genericErrorMessage);
    }

    [Fact]
    public void LogError_With_Message_Should_Write_To_Console_Error()
    {
        // Act
        _octoLogger.LogError("message");

        // Assert
        _consoleOutput.Should().BeNull();
        _consoleError.Should().NotBeNull();
    }

    [Fact]
    public void LogError_With_Exception_Should_Write_To_Console_Error()
    {
        // Act
        _octoLogger.LogError(new ArgumentNullException("arg"));

        // Assert
        _consoleOutput.Should().BeNull();
        _consoleError.Should().NotBeNull();
    }

    [Fact]
    public void LogInformation_Should_Write_To_Console_Out()
    {
        // Act
        _octoLogger.LogInformation("message");

        // Assert
        _consoleOutput.Should().NotBeNull();
        _consoleError.Should().BeNull();
    }

    [Fact]
    public void LogWarning_Should_Write_To_Console_Out()
    {
        // Act
        _octoLogger.LogWarning("message");

        // Assert
        _consoleOutput.Should().NotBeNull();
        _consoleError.Should().BeNull();
    }

    [Fact]
    public void LogVerbose_Should_Write_To_Console_Out_In_Verbose_Mode()
    {
        // Arrange
        _octoLogger.Verbose = true;

        // Act
        _octoLogger.LogVerbose("message");

        // Assert
        _consoleOutput.Should().NotBeNull();
        _consoleError.Should().BeNull();
    }

    [Fact]
    public void Verbose_Log_Should_Capture_Http_Status_Code()
    {
        // Arrange
        _octoLogger.Verbose = true;
        var ex = new HttpRequestException(null, null, HttpStatusCode.BadGateway); // HTTP 502

        // Act
        _octoLogger.LogError(ex);

        // Assert
        _verboseLogOutput.Trim().Should().Contain("502");
    }

    private void CaptureLogOutput(string msg) => _logOutput += msg;

    private void CaptureVerboseLogOutput(string msg) => _verboseLogOutput += msg;

    private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;

    private void CaptureConsoleError(string msg) => _consoleError += msg;
}
