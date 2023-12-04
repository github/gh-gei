using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public static class TestHelpers
    {
        private static readonly object _mutex = new();

        public const string CLI_ROOT_COMMAND = "ROOT_COMMAND";
        public const string CLI_EXECUTING_COMMAND = "EXECUTING_COMMAND";

        public static Mock<T> CreateMock<T>() where T : class
        {
            var ctor = typeof(T).GetConstructors().First();
            var argCount = ctor.GetParameters().Length;
            var args = new object[argCount];

            return new Mock<T>(args);
        }

        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required, bool isHidden = false)
        {
            var option = options.Single(x => x.Name == name);

            Assert.Equal(required, option.IsRequired);
            Assert.Equal(isHidden, option.IsHidden);
        }

        public static void SetCliContext()
        {
            lock (_mutex)
            {
                CliContext.RootCommand ??= CLI_ROOT_COMMAND;
                CliContext.ExecutingCommand ??= CLI_EXECUTING_COMMAND;
            }
        }
    }
}
