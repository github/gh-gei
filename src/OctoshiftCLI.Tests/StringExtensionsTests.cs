using FluentAssertions;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public sealed class StringExtensionsTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("Parts Unlimited", "Parts-Unlimited")]
        [InlineData("Parts-Unlimited", "Parts-Unlimited")]
        [InlineData("Parts@@@@Unlimited", "Parts-Unlimited")]
        public void ReplaceInvalidCharactersWithDash_Returns_Valid_String(string value, string expectedValue)
        {
            var normalizedValue = value.ReplaceInvalidCharactersWithDash();

            normalizedValue.Should().Be(expectedValue);
        }
    }
}
