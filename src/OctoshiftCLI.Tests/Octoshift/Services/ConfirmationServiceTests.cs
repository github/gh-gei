using System;
using System.Text;
using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services
{
    public class ConfirmationServiceTests
    {
        private readonly ConfirmationService _confirmationService;
        private readonly string confirmationPrompt;
        private readonly string cancelationOutput;
        private readonly string confirmationOutput;
        private string _consoleOutput;
        private int _exitOutput;
        private int numOfCalls;
        private ConsoleKey passedKey;

        public ConfirmationServiceTests()
        {
            _confirmationService = new ConfirmationService(CaptureConsoleOutput, MockConsoleKeyPress, CancelCommand);
            confirmationPrompt = "Are you sure you wish to continue? [y/N]";
            cancelationOutput = "Command Cancelled.";
            confirmationOutput = "Confirmation Recorded. Proceeding...";
        }

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
            _confirmationService.AskForConfirmation(confirmationPrompt);

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
            _exitOutput.Should().Be(0);
        }

        [Fact]
        public void AskForConfirmation_Should_Exit_With_Provided_ErrorMessage()
        {
            // Arrange
            passedKey = ConsoleKey.N;
            numOfCalls = 3;
            var failureReason = "You made me fail.";
            var expectedResult = confirmationPrompt + cancelationOutput + " " + failureReason;

            // Act
            _confirmationService.AskForConfirmation(confirmationPrompt, failureReason);

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
            _exitOutput.Should().Be(0);
        }

        [Fact]
        public void AskForConfirmation_Should_Ask_Again_On_Wrong_KeyPress()
        {
            // Arrange
            passedKey = ConsoleKey.Enter;
            numOfCalls = 3;
            var expectedResult = new StringBuilder().Insert(0, confirmationPrompt, numOfCalls + 1) + cancelationOutput;


            // Act
            _confirmationService.AskForConfirmation(confirmationPrompt);

            // Assert
            _consoleOutput.Trim().Should().BeEquivalentTo(expectedResult);
            _exitOutput.Should().Be(0);
        }

        private void CaptureConsoleOutput(string msg, ConsoleColor outputColor) => _consoleOutput += msg;

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

        private void CancelCommand(int exitCode) => _exitOutput = exitCode;
    }
}

