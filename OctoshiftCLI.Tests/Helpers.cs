using System;
using System.CommandLine;
//using System.CommandLine.Invocation;
using System.Collections.Generic;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class Helpers
    {
        //
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required)
        {
            foreach (var option in options)
            {
                if (option.Name == name)
                {
                    Assert.Equal(required, option.IsRequired);
                    return;
                }
            }
            Assert.True(false, $"Option '{name}' not found");
        }
    }
}