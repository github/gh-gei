using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public static class TestHelpers
    {
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required, bool isHidden = false)
        {
            var option = options.Single(x => x.Name == name);

            Assert.Equal(required, option.IsRequired);
            Assert.Equal(isHidden, option.IsHidden);
        }
    }
}
