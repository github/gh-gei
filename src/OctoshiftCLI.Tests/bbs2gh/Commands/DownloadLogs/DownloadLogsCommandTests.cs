using System.Linq;
using OctoshiftCLI.BbsToGithub.Commands.DownloadLogs;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands.DownloadLogs;

public class DownloadLogsCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new DownloadLogsCommand();
        Assert.NotNull(command);
        Assert.Equal("download-logs", command.Name);
        Assert.Equal(8, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-repo", false);
        TestHelpers.VerifyCommandOption(command.Options, "migration-id", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "migration-log-file", false);
        TestHelpers.VerifyCommandOption(command.Options, "overwrite", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }

    [Fact]
    public void Should_Support_Github_Api_Url_Alias_For_Backward_Compatibility()
    {
        // Test that --github-api-url still works as an alias for --target-api-url
        var command = new DownloadLogsCommand();
        var option = command.Options.FirstOrDefault(o => o.Name == "target-api-url");

        Assert.NotNull(option);
        Assert.Contains("--target-api-url", option.Aliases);
        Assert.Contains("--github-api-url", option.Aliases);
    }
}
