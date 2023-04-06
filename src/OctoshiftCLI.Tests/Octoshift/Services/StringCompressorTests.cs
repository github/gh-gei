using FluentAssertions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class StringCompressorTests
{
    [Fact]
    public void GZipAndBase64String_Correctly_Compresses_String()
    {
        var uncompressed = "uncompressed_test_string";
        // It seems like, depending on the underlying OS, two characters in the Zipped and Base64 encoded string differ.
        // So we simply check for all variants here to cover Linux, Mac OSX and Windows.
        var expectedStringPattern = "H4sIAAAAAAAA(Ey|Ci|Ay)vNS87PLShKLS5OTYkvSS0uiS8uKcrMSwcAdqGS8xgAAAA=";

        var actualString = StringCompressor.GZipAndBase64String(uncompressed);

        actualString.Should().MatchRegex(expectedStringPattern);
    }
}
