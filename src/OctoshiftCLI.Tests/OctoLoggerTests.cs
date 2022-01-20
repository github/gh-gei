using System;
using FluentAssertions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class OctoLoggerTests
    {
        private string _logOutput;
        private string _verboseLogOutput;
        private string _consoleOutput;
        private string _consoleError;

        [Fact]
        public void Secrets_Should_Be_Masked_From_Logs_And_Console()
        {
            var secret = "purplemonkeydishwasher";

            var sut = new OctoLogger(CaptureLogOutput, CaptureVerboseLogOutput, CaptureConsoleOutput, CaptureConsoleError);

            sut.RegisterSecret(secret);

            sut.Verbose = false;
            sut.LogInformation($"Don't tell anybody that {secret} is my password");
            sut.LogVerbose($"Don't tell anybody that {secret} is my password");
            sut.LogWarning($"Don't tell anybody that {secret} is my password");
            sut.LogSuccess($"Don't tell anybody that {secret} is my password");
            sut.LogError($"Don't tell anybody that {secret} is my password");
            sut.LogError(new OctoshiftCliException($"Don't tell anybody that {secret} is my password"));
            sut.LogError(new InvalidOperationException($"Don't tell anybody that {secret} is my password"));

            sut.Verbose = true;
            sut.LogVerbose($"Don't tell anybody that {secret} is my password");

            _consoleOutput.Should().NotContain(secret);
            _logOutput.Should().NotContain(secret);
            _verboseLogOutput.Should().NotContain(secret);
            _consoleError.Should().NotContain(secret);
        }

        [Fact]
        public void LogError_For_OctoshiftCliException_Should_Log_Exception_Message_In_Non_Verbose_Mode()
        {
            // Arrange
            string console = null;
            string log = null;
            string verbose = null;
            string error = null;

            var logger = new OctoLogger(msg => log = msg, msg => verbose = msg, msg => console = msg, msg => error = msg)
            {
                Verbose = false
            };

            const string userFriendlyMessage = "A user friendly message";
            const string exceptionDetails = "exception details";
            var octoshiftCliException = new OctoshiftCliException(userFriendlyMessage,
                new ArgumentNullException("arg", exceptionDetails));

            // Act
            logger.LogError(octoshiftCliException);

            // Assert
            console.Should().BeNull();

            error.Trim().Should().EndWith($"[ERROR] {userFriendlyMessage}");
            error.Should().NotContain(exceptionDetails);

            log.Trim().Should().EndWith($"[ERROR] {userFriendlyMessage}");
            log.Should().NotContain(exceptionDetails);

            verbose.Trim().Should().EndWith($"[ERROR] {octoshiftCliException}");
        }

        [Fact]
        public void LogError_For_Unexpected_Exception_Should_Log_Generic_Error_Message_In_Non_Verbose_Mode()
        {
            // Arrange
            const string genericErrorMessage = "An unexpected error happened. Please see the logs for details.";

            string console = null;
            string log = null;
            string verbose = null;
            string error = null;

            var logger = new OctoLogger(msg => log = msg, msg => verbose = msg, msg => console = msg, msg => error = msg)
            {
                Verbose = false
            };

            const string userEnemyMessage = "Some user enemy error message!";
            const string exceptionDetails = "exception details";
            var unexpectedException = new InvalidOperationException(userEnemyMessage,
                new ArgumentNullException("arg", exceptionDetails));

            // Act
            logger.LogError(unexpectedException);

            // Assert
            console.Should().BeNull();

            error.Trim().Should().EndWith($"[ERROR] {genericErrorMessage}");
            error.Should().NotContain(userEnemyMessage);
            error.Should().NotContain(exceptionDetails);

            log.Trim().Should().EndWith($"[ERROR] {genericErrorMessage}");
            log.Should().NotContain(userEnemyMessage);
            log.Should().NotContain(exceptionDetails);

            verbose.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
            verbose.Should().NotContain(genericErrorMessage);
        }

        [Fact]
        public void LogError_For_Any_Exception_Should_Always_Log_Entire_Exception_In_Verbose_Mode()
        {
            // Arrange
            const string genericErrorMessage = "An unexpected error happened. Please see the logs for details.";

            string console = null;
            string log = null;
            string verbose = null;
            string error = null;

            var logger = new OctoLogger(msg => log = msg, msg => verbose = msg, msg => console = msg, msg => error = msg)
            {
                Verbose = true
            };

            var unexpectedException =
                new InvalidOperationException("Some user enemy error message!", new ArgumentNullException("arg"));

            // Act
            logger.LogError(unexpectedException);

            // Assert
            console.Should().BeNull();

            error.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
            error.Should().NotContain(genericErrorMessage);

            log.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
            log.Should().NotContain(genericErrorMessage);

            verbose.Trim().Should().EndWith($"[ERROR] {unexpectedException}");
            verbose.Should().NotContain(genericErrorMessage);
        }

        [Fact]
        public void LogError_With_Message_Should_Write_To_Console_Error()
        {
            // Arrange
            string console = null;
            string error = null;

            var logger = new OctoLogger(_ => { }, _ => { }, msg => console = msg, msg => error = msg);

            // Act
            logger.LogError("message");

            // Assert
            console.Should().BeNull();
            error.Should().NotBeNull();
        }

        [Fact]
        public void LogError_With_Exception_Should_Write_To_Console_Error()
        {
            // Arrange
            string console = null;
            string error = null;

            var logger = new OctoLogger(_ => { }, _ => { }, msg => console = msg, msg => error = msg);

            // Act
            logger.LogError(new ArgumentNullException("arg"));

            // Assert
            console.Should().BeNull();
            error.Should().NotBeNull();
        }

        [Fact]
        public void LogInformation_Should_Write_To_Console_Out()
        {
            // Arrange
            string console = null;
            string error = null;

            var logger = new OctoLogger(_ => { }, _ => { }, msg => console = msg, msg => error = msg);

            // Act
            logger.LogInformation("message");

            // Assert
            console.Should().NotBeNull();
            error.Should().BeNull();
        }

        [Fact]
        public void LogWarning_Should_Write_To_Console_Out()
        {
            // Arrange
            string console = null;
            string error = null;

            var logger = new OctoLogger(_ => { }, _ => { }, msg => console = msg, msg => error = msg);

            // Act
            logger.LogWarning("message");

            // Assert
            console.Should().NotBeNull();
            error.Should().BeNull();
        }

        [Fact]
        public void LogVerbose_Should_Write_To_Console_Out_In_Verbose_Mode()
        {
            // Arrange
            string console = null;
            string error = null;

            var logger = new OctoLogger(_ => { }, _ => { }, msg => console = msg, msg => error = msg)
            {
                Verbose = true
            };

            // Act
            logger.LogVerbose("message");

            // Assert
            console.Should().NotBeNull();
            error.Should().BeNull();
        }

        private void CaptureLogOutput(string msg) => _logOutput += msg;

        private void CaptureVerboseLogOutput(string msg) => _verboseLogOutput += msg;

        private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;

        private void CaptureConsoleError(string msg) => _consoleError += msg;
    }
}