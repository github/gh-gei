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

        [Theory]
        [InlineData("https://github.com/my-org", true)]
        [InlineData("http://github.com/my-org", true)]
        [InlineData("https://github.com/my-org/my-repo", true)]
        [InlineData("http://example.com", true)]
        [InlineData("www.github.com", false)]
        [InlineData("github.com/my-org", false)]
        [InlineData("my-org", false)]
        [InlineData("my-repo", false)]
        [InlineData("my-org-123", false)]
        [InlineData("my_repo", false)]
        [InlineData("MyOrganization", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("   ", false)]
        public void IsUrl_Detects_URLs_Correctly(string value, bool expectedResult)
        {
            var result = value.IsUrl();

            result.Should().Be(expectedResult);
        }
    }
}
