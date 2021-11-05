using System;

namespace OctoshiftCLI
{
    public class OctoLogger
    {
        public bool Verbose { get; set; }

        public void Log(string msg)
        {
            var output = $"[{DateTime.Now.ToShortTimeString()}] [INFO] {msg}";
            Console.WriteLine(output);
        }

        public void LogInformation(string msg) => Log(msg);

        public void LogWarning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(msg);
            Console.ResetColor();
        }

        public void LogError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(msg);
            Console.ResetColor();
        }

        public void LogError(string msg, Exception ex)
        {
            // TODO: include details from the exception in the logs
            throw new NotImplementedException();
        }

        public void LogError(Exception ex)
        {
            throw new NotImplementedException();
        }

        public void LogVerbose(string msg)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Log(msg);
                Console.ResetColor();
            }
        }

        public void LogSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(msg);
            Console.ResetColor();
        }
    }
}
