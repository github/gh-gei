using System;
namespace OctoshiftCLI.Services
{
    public class ConfirmationService
    {
        # region Variables

        private readonly Action<string> _writeToConsoleOut;
        private readonly Func<ConsoleKey> _readConsoleKey;

        #endregion

        #region Constructors
        public ConfirmationService()
        {
            _writeToConsoleOut = msg => Console.WriteLine(msg);
            _readConsoleKey = ReadKey;
        }

        // Constructor designed to allow for testing console methods
        public ConfirmationService(Action<string> writeToConsoleOut, Func<ConsoleKey> readConsoleKey)
        {
            _writeToConsoleOut = writeToConsoleOut;
            _readConsoleKey = readConsoleKey;
        }

        #endregion

        #region Functions
        public bool AskForConfirmation(string confirmationPrompt, string cancellationErrorMessage = "")
        {
            ConsoleKey response;
            do
            {
                _writeToConsoleOut(confirmationPrompt);
                response = _readConsoleKey();
                if (response != ConsoleKey.Enter)
                {
                    _writeToConsoleOut("");
                }

            } while (response is not ConsoleKey.Y and not ConsoleKey.N);

            if (response == ConsoleKey.Y)
            {
                _writeToConsoleOut("Confirmation Recorded. Proceeding...");
                return true;
            }
            else
            {
                _writeToConsoleOut("Canceling Command...");
                throw new OctoshiftCliException($"Command Cancelled. {cancellationErrorMessage}");
            }
        }

        private ConsoleKey ReadKey()
        {
            return Console.ReadKey(false).Key;
        }
        #endregion
    }
}

