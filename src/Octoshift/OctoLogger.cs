using System;
using System.Collections.Generic;
using System.IO;

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
        private readonly HashSet<string> _secrets = new HashSet<string>();
        private readonly string _logFilePath;
        private readonly string _verboseFilePath;

        private readonly Action<string> _writeToLog;
        private readonly Action<string> _writeToVerboseLog;
        private readonly Action<string> _writeToConsole;

        public OctoLogger()
        {
            var logStartTime = DateTime.Now;
            _logFilePath = $"{logStartTime:yyyyMMddHHmmss}.octoshift.log";
            _verboseFilePath = $"{logStartTime:yyyyMMddHHmmss}.octoshift.verbose.log";

            _writeToLog = msg => File.AppendAllText(_logFilePath, msg);
            _writeToVerboseLog = msg => File.AppendAllText(_verboseFilePath, msg);
            _writeToConsole = msg => Console.Write(msg);
        }

        public OctoLogger(Action<string> writeToLog, Action<string> writeToVerboseLog, Action<string> writeToConsole)
        {
            _writeToLog = writeToLog;
            _writeToVerboseLog = writeToVerboseLog;
            _writeToConsole = writeToConsole;
        }

        private void Log(string msg, string level)
        {
            var output = FormatMessage(msg, level);
            output = MaskSecrets(output);
            _writeToConsole(output);
            _writeToLog(output);
            _writeToVerboseLog(output);
        }

        private string FormatMessage(string msg, string level) => $"[{DateTime.Now.ToShortTimeString()}] [{level}] {msg}\n";

        private string MaskSecrets(string msg)
        {
            var result = msg;

            foreach (var secret in _secrets)
            {
                result = result.Replace(secret, "***");
            }

            return result;
        }

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
                _writeToVerboseLog(MaskSecrets(FormatMessage(msg, LogLevel.VERBOSE)));
            }
        }

        public virtual void LogSuccess(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(msg, LogLevel.SUCCESS);
            Console.ResetColor();
        }

        public virtual void RegisterSecret(string secret) => _secrets.Add(secret);
    }
}