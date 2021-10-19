using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class Helpers
    {
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required)
        {
            var option = options.Single(x => x.Name == name);
            
            Assert.Equal(required, option.IsRequired);
        }
    }
}
