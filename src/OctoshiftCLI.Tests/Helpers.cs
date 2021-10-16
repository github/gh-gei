using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class Helpers
    {
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required)
        {
            var option = options.First(x => x.Name == name);
            
            Assert.True(option != null, $"Option {name} not found");
            Assert.Equal(required, option.IsRequired);
        }
    }
}