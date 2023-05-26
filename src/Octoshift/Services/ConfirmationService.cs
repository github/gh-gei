using System;
namespace OctoshiftCLI.Services
{
    public class ConfirmationService
    {
        # region Variables

        private readonly Action<string> _writeToConsoleOut;
        private readonly Func<ConsoleKey> _readConsoleKey;
        private readonly Action<int> _cancelCommand;

        #endregion

        #region Constructors
        public ConfirmationService()
        {
            _writeToConsoleOut = msg => Console.WriteLine(msg);
            _readConsoleKey = ReadKey;
            _cancelCommand = code => Environment.Exit(code);
        }

        // Constructor designed to allow for testing console methods
        public ConfirmationService(Action<string> writeToConsoleOut, Func<ConsoleKey> readConsoleKey, Action<int> cancelCommand)
        {
            _writeToConsoleOut = writeToConsoleOut;
            _readConsoleKey = readConsoleKey;
            _cancelCommand = cancelCommand;
        }

        #endregion

        #region Functions
        public virtual bool AskForConfirmation(string confirmationPrompt, string cancellationErrorMessage = "")
        {
            ConsoleKey response;
            do
            {
                Console.ForegroundColor = ConsoleColor.Yellow; //Used to distinguish confirmation warning
                _writeToConsoleOut(confirmationPrompt);
                Console.ForegroundColor = ConsoleColor.White;
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
                _writeToConsoleOut($"Command Cancelled. {cancellationErrorMessage}");
                _cancelCommand(0);
            }
            return false;
        }

        private ConsoleKey ReadKey()
        {
            return Console.ReadKey(false).Key;
        }
        #endregion
    }
}

