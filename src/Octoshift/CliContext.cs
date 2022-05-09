using System;

namespace OctoshiftCLI
{
    public static class CliContext
    {
        private static string _rootCommand;
        private static string _executingCommand;

        public static string RootCommand
        {
            set => _rootCommand = _rootCommand is null
                ? value
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
    }
}
