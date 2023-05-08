using System;
using System.Text;
using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class ConfirmationServiceTests
    {
        #region Variables
        private readonly ConfirmationService _confirmationService;
        private readonly string confirmationPrompt;
        private readonly string cancelationOutput;
        private readonly string confirmationOutput;
        private string _consoleOutput;
        private int numOfCalls;
        private ConsoleKey passedKey;
        #endregion

        #region Constructor
        public ConfirmationServiceTests()
        {
            _confirmationService = new ConfirmationService(CaptureConsoleOutput, MockConsoleKeyPress);
            confirmationPrompt = "Are you sure you wish to continue? Y/N?";
            cancelationOutput = "Canceling Command...";
            confirmationOutput = "Confirmation Recorded. Proceeding...";
        }
        #endregion

        #region Tests
        [Fact]
        public void AskForConfirmation_Happy_Path()
        {
            // Arrange
            passedKey = ConsoleKey.Y;
            numOfCalls = 3;
            var expectedResult = confirmationPrompt + confirmationOutput;

            // Act
            _confirmationService.AskForConfirmation(confirmationPrompt);

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public void AskForConfirmation_Should_Exit_With_N_Keypress()
        {
            // Arrange
            passedKey = ConsoleKey.N;
            numOfCalls = 3;
            var expectedResult = confirmationPrompt + cancelationOutput;

            // Act
            Assert.Throws<OctoshiftCliException>(() => _confirmationService.AskForConfirmation(confirmationPrompt));

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public void AskForConfirmation_Should_Exit_With_Provided_ErrorMessage()
        {
            // Arrange
            passedKey = ConsoleKey.N;
            numOfCalls = 3;
            var failureReason = "You made me fail.";
            var expectedResult = confirmationPrompt + cancelationOutput;

            // Act
            var exception = Assert.Throws<OctoshiftCliException>(() => _confirmationService.AskForConfirmation(confirmationPrompt, failureReason));

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
            Assert.Equal($"Command Cancelled. {failureReason}", exception.Message);
        }

        [Fact]
        public void AskForConfirmation_Should_Ask_Again_On_Wrong_KeyPress()
        {
            // Arrange
            passedKey = ConsoleKey.Enter;
            numOfCalls = 3;
            var expectedResult = new StringBuilder().Insert(0, confirmationPrompt, numOfCalls) + cancelationOutput;


            // Act
            Assert.Throws<OctoshiftCliException>(() => _confirmationService.AskForConfirmation(confirmationPrompt));

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
        }
        #endregion

        #region Private functions
        private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;

        private ConsoleKey MockConsoleKeyPress()
        {
            // Prevents infinity loop in testing
            if (numOfCalls <= 0)
            {
                return ConsoleKey.N;
            }
            numOfCalls--;
            return passedKey;
        }
        #endregion

    }
}

