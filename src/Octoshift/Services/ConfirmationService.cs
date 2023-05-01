using System;
namespace OctoshiftCLI.Services
{
    public class ConfirmationService
    {

        private readonly Action<string> _writeToConsoleOut;

        public ConfirmationService()
        {
            _writeToConsoleOut = msg => Console.Write(msg);
        }

        public void AskForConfirmation(string confirmationPrompt)
        {
            bool confirmed = false;
            do
            {
                ConsoleKey response;
                do
                {
                    _writeToConsoleOut(confirmationPrompt);
                    response = Console.ReadKey(false).Key;
                    if (response != ConsoleKey.Enter)
                        Console.WriteLine();

                } while (response != ConsoleKey.Y && response != ConsoleKey.N);

                confirmed = response == ConsoleKey.Y;
            } while (!confirmed);
            // TODO: Get verbiage approved
            Console.WriteLine("Confirmation Recorded. Proceeding...");
        }
    }
}

