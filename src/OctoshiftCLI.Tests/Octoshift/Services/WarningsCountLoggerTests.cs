using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class WarningsCountLoggerTests
{
    private string _logOutput;
    private string _verboseLogOutput;
    private string _consoleOutput;
    private string _consoleError;

    private readonly OctoLogger _octoLogger;
    private readonly WarningsCountLogger _warningsCountLogger;

    public WarningsCountLoggerTests()
    {
        _octoLogger = new OctoLogger(CaptureLogOutput, CaptureVerboseLogOutput, CaptureConsoleOutput, CaptureConsoleError);
        _warningsCountLogger = new WarningsCountLogger(_octoLogger);
    }

    [Fact]
    public void LogWarningsCount_Should_Write_1_Warning_To_Console_Out()
    {
        _warningsCountLogger.LogWarningsCount(1);

        _consoleOutput.Should().Contain("1 warning encountered during this migration");
    }

    [Fact]
    public void LogWarningsCount_Should_Write_Warnings_Count_To_Console_Out()
    {
        _warningsCountLogger.LogWarningsCount(3);

        _consoleOutput.Should().Contain($"3 warnings encountered during this migration");
    }

    private void CaptureLogOutput(string msg) => _logOutput += msg;

    private void CaptureVerboseLogOutput(string msg) => _verboseLogOutput += msg;

    private void CaptureConsoleOutput(string msg) => _consoleOutput += msg;

    private void CaptureConsoleError(string msg) => _consoleError += msg;
}
