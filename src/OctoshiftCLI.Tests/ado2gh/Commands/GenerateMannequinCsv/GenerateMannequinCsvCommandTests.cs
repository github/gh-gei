using OctoshiftCLI.AdoToGithub.Commands.GenerateMannequinCsv;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GenerateMannequinCsvCommand();
        Assert.NotNull(command);
        Assert.Equal("generate-mannequin-csv", command.Name);
        Assert.Equal(6, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "output", false);
        TestHelpers.VerifyCommandOption(command.Options, "include-reclaimed", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
    }
}
