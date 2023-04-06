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

        _octoLogger.RegisterSecret(secret);

        _octoLogger.Verbose = false;
        _octoLogger.LogInformation($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogVerbose($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogWarning($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogSuccess($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogError($"Don't tell anybody that {secret} is my password");
        _octoLogger.LogError(new OctoshiftCliException($"Don't tell anybody that {secret} is my password"));
        _octoLogger.LogError(new InvalidOperationException($"Don't tell anybody that {secret} is my password"));

        _octoLogger.Verbose = true;
        _octoLogger.LogVerbose($"Don't tell anybody that {secret} is my password");

        _consoleOutput.Should().NotContain(secret);
        _logOutput.Should().NotContain(secret);
        _verboseLogOutput.Should().NotContain(secret);
        _consoleError.Should().NotContain(secret);
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
