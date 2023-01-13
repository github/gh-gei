using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands;

public class DownloadLogsCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new DownloadLogsCommand();
        Assert.NotNull(command);
        Assert.Equal("download-logs", command.Name);
        Assert.Equal(7, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
        TestHelpers.VerifyCommandOption(command.Options, "github-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "migration-log-file", false);
        TestHelpers.VerifyCommandOption(command.Options, "overwrite", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
