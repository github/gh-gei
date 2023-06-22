using System;
namespace OctoshiftCLI.Services
{
    public class ConfirmationService
    {
        private readonly Action<string, ConsoleColor> _writeToConsoleOut;
        private readonly Func<ConsoleKey> _readConsoleKey;
        private readonly Action<int> _cancelCommand;

        public ConfirmationService()
        {
            _writeToConsoleOut = (msg, outputColor) =>
            {
                Console.ForegroundColor = outputColor;
                Console.WriteLine(msg);
            };
            _readConsoleKey = ReadKey;
            _cancelCommand = code => Environment.Exit(code);
        }

        // Constructor designed to allow for testing console methods
        public ConfirmationService(Action<string, ConsoleColor> writeToConsoleOut, Func<ConsoleKey> readConsoleKey, Action<int> cancelCommand)
        {
            _writeToConsoleOut = writeToConsoleOut;
            _readConsoleKey = readConsoleKey;
            _cancelCommand = cancelCommand;
        }

        public virtual bool AskForConfirmation(string confirmationPrompt, string cancellationErrorMessage = "")
        {
            ConsoleKey response;
            do
            {
                _writeToConsoleOut(confirmationPrompt, ConsoleColor.Yellow);
                Console.ForegroundColor = ConsoleColor.White;
                response = _readConsoleKey();
                if (response != ConsoleKey.Enter)
                {
                    _writeToConsoleOut("", ConsoleColor.White);
                }

            } while (response is not ConsoleKey.Y and not ConsoleKey.N);

            if (response == ConsoleKey.Y)
            {
                _writeToConsoleOut("Confirmation Recorded. Proceeding...", ConsoleColor.White);
                return true;
            }
            else
            {
                _writeToConsoleOut($"Command Cancelled. {cancellationErrorMessage}", ConsoleColor.White);
                _cancelCommand(0);
            }
            return false;
        }

        private ConsoleKey ReadKey()
        {
            return Console.ReadKey(false).Key;
        }
    }
}

