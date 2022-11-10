using System.Threading.Tasks;
using Octoshift;
using Xunit;

namespace OctoshiftCLI.Tests;

public class StringCompressorTests
{
    [Fact]
    public void GZipAndBase64String_correctly_compresses_string()
    {
        var uncompressed = "uncompressed_test_string";
        var expectedString = "H4sIAAAAAAAAEyvNS87PLShKLS5OTYkvSS0uiS8uKcrMSwcAdqGS8xgAAAA=";

        var actualString = StringCompressor.GZipAndBase64String(uncompressed);

        Assert.Equal(expectedString, actualString);
    }
}
