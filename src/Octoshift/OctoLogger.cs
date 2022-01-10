namespace OctoshiftCLI
{
    internal static class LogLevel
    {
        public const string INFO = "INFO";
        public const string WARNING = "WARNING";
        public const string ERROR = "ERROR";
        public const string SUCCESS = "INFO";
        public const string VERBOSE = "DEBUG";
    }

    public class OctoLogger
    {
        public bool Verbose { get; set; }
        private readonly DateTime _logStartTime;
        private readonly string _logFilePath;
        private readonly string _verboseFilePath;

        public OctoLogger()
        {
            _logStartTime = DateTime.Now;
            _logFilePath = $"{_logStartTime:yyyyMMddHHmmss}.octoshift.log";
            _verboseFilePath = $"{_logStartTime:yyyyMMddHHmmss}.octoshift.verbose.log";

            // TODO: Open the file once and keep it open
        }

        private void Log(string msg, string level)
        {
            var output = FormatMessage(msg, level);
            Console.Write(output);
            File.AppendAllText(_logFilePath, output);
            File.AppendAllText(_verboseFilePath, output);
        }

        private string FormatMessage(string msg, string level) => $"[{DateTime.Now.ToShortTimeString()}] [{level}] {msg}\n";

        public virtual void LogInformation(string msg) => Log(msg, LogLevel.INFO);

        public virtual void LogWarning(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(msg, LogLevel.WARNING);
            Console.ResetColor();
        }

        public virtual void LogError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(msg, LogLevel.ERROR);
            Console.ResetColor();
        }

        public virtual void LogError(string msg, Exception ex)
        {
            // TODO: include details from the exception in the logs
            throw new NotImplementedException();
        }

        public virtual void LogError(Exception ex)
        {
            throw new NotImplementedException();
        }

        public virtual void LogVerbose(string msg)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Log(msg, LogLevel.VERBOSE);
                Console.ResetColor();
            }
            else
            {
                File.AppendAllText(_verboseFilePath, FormatMessage(msg, LogLevel.VERBOSE));
            }
        }

        public virtual void LogSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(msg, LogLevel.SUCCESS);
            Console.ResetColor();
        }
    }
}