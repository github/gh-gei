using System.CommandLine;

namespace OctoshiftCLI.Extensions;

public static class CommandLineOptionExtensions
{
    public static string GetLogFriendlyName(this Option option) => option?.Name.ToUpper().Replace("-", " ");
}
