using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace OctoshiftCLI.Services;

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
    public virtual bool Verbose { get; set; }
    private readonly HashSet<string> _secrets = [];
    private readonly string _logFilePath;
    private readonly string _verboseFilePath;
    private readonly bool _debugMode;
    private readonly object _mutex = new();

    private readonly Action<string> _writeToLog;
    private readonly Action<string> _writeToVerboseLog;
    private readonly Action<string> _writeToConsoleOut;
    private readonly Action<string> _writeToConsoleError;

    private const string GENERIC_ERROR_MESSAGE = "An unexpected error happened. Please see the logs for details.";

    private readonly List<string> _redactionPatterns =
    [
        // General purpose "Don't include the token"
        "\\b(?<=token=)([^&]+?)\\b",
        // AWS SIGv4 credential
        "\\b(?<=X-Amz-Credential=)([^&]+?)\\b",
        // Azure Blob Store SAS URL signature
        "\\b(?<=sig=)([^&]+?)\\b",
    ];

    public OctoLogger()
    {
        var logStartTime = DateTime.Now;
        var processId = Environment.ProcessId;
        _logFilePath = $"{logStartTime:yyyyMMddHHmmss}-{processId}.octoshift.log";
        _verboseFilePath = $"{logStartTime:yyyyMMddHHmmss}-{processId}.octoshift.verbose.log";

        if (Environment.GetEnvironmentVariable("GEI_DEBUG_MODE")?.ToUpperInvariant() == "TRUE")
        {
            _debugMode = true;
        }

        _writeToLog = msg => File.AppendAllText(_logFilePath, msg);
        _writeToVerboseLog = msg => File.AppendAllText(_verboseFilePath, msg);
        _writeToConsoleOut = msg => Console.Write(msg);
        _writeToConsoleError = msg => Console.Error.Write(msg);
    }

    public OctoLogger(Action<string> writeToLog, Action<string> writeToVerboseLog, Action<string> writeToConsoleOut, Action<string> writeToConsoleError)
    {
        _writeToLog = writeToLog;
        _writeToVerboseLog = writeToVerboseLog;
        _writeToConsoleOut = writeToConsoleOut;
        _writeToConsoleError = writeToConsoleError;
    }

    private void Log(string msg, string level)
    {
        var output = FormatMessage(msg, level);
        output = Redact(output);
        if (level == LogLevel.ERROR)
        {
            _writeToConsoleError(output);
        }
        else
        {
            _writeToConsoleOut(output);
        }
        _writeToLog(output);
        _writeToVerboseLog(output);
    }

    private string FormatMessage(string msg, string level)
    {
        var timeFormat = _debugMode ? DateTime.Now.ToString("o") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return $"[{timeFormat}] [{level}] {msg}\n";
    }

    private string Redact(string msg)
    {
        var result = msg;

        foreach (var secret in _secrets.Where(x => x is not null))
        {
            result = result.Replace(secret, "***")
                .Replace(Uri.EscapeDataString(secret), "***");
        }

        foreach (var redactionPattern in _redactionPatterns)
        {
            result = Regex.Replace(result, redactionPattern, "***", RegexOptions.IgnoreCase);
        }

        return result;
    }

    public virtual void LogInformation(string msg)
    {
        lock (_mutex)
        {
            Log(msg, LogLevel.INFO);
        }
    }

    public virtual void LogWarning(string msg)
    {
        lock (_mutex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(msg, LogLevel.WARNING);
            Console.ResetColor();
        }
    }

    public virtual void LogError(string msg)
    {
        lock (_mutex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(msg, LogLevel.ERROR);
            Console.ResetColor();
        }
    }

    public virtual void LogError(Exception ex)
    {
        if (ex is null)
        {
            throw new ArgumentNullException(nameof(ex));
        }

        lock (_mutex)
        {
            var verboseMessage = ex is HttpRequestException httpEx ? $"[HTTP ERROR {(int?)httpEx.StatusCode}] {ex}" : ex.ToString();
            var logMessage = Verbose ? verboseMessage : ex is OctoshiftCliException ? ex.Message : GENERIC_ERROR_MESSAGE;

            var output = Redact(FormatMessage(logMessage, LogLevel.ERROR));

            Console.ForegroundColor = ConsoleColor.Red;
            _writeToConsoleError(output);
            Console.ResetColor();

            _writeToLog(output);
            _writeToVerboseLog(Redact(FormatMessage(verboseMessage, LogLevel.ERROR)));
        }
    }

    public virtual void LogVerbose(string msg)
    {
        lock (_mutex)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Log(msg, LogLevel.VERBOSE);
                Console.ResetColor();
            }
            else
            {
                _writeToVerboseLog(Redact(FormatMessage(msg, LogLevel.VERBOSE)));
            }
        }
    }

    public virtual void LogDebug(string msg)
    {
        if (_debugMode)
        {
            LogVerbose(msg);
        }
    }

    public virtual void LogSuccess(string msg)
    {
        lock (_mutex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(msg, LogLevel.SUCCESS);
            Console.ResetColor();
        }
    }

    public virtual void RegisterSecret(string secret) => _secrets.Add(secret);
}
