using System;
using System.Text.RegularExpressions;

namespace OctoshiftCLI
{
    public static class CliContext
    {
        private static string _rootCommand;
        private static string _executingCommand;

        public static string RootCommand
        {
            set => _rootCommand = _rootCommand is null
                ? Regex.Replace(value, @"^gh-", string.Empty)
                : throw new InvalidOperationException("Value can only be set once.");
            get => _rootCommand;
        }

        public static string ExecutingCommand
        {
            set => _executingCommand = _executingCommand is null
                ? value
                : throw new InvalidOperationException("Value can only be set once.");
            get => _executingCommand;
        }

        public static void Clear()
        {
            _rootCommand = null;
            _executingCommand = null;
        }
    }
}
