using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class GenerateMannequinCsvCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new GenerateMannequinCsvCommand(null, null);
        Assert.NotNull(command);
        Assert.Equal("generate-mannequin-csv", command.Name);
        Assert.Equal(5, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "output", false);
        TestHelpers.VerifyCommandOption(command.Options, "include-reclaimed", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
