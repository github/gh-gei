using OctoshiftCLI.GithubEnterpriseImporter.Commands.DownloadLogs;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.DownloadLogs;

public class DownloadLogsCommandTests
{
    [Fact]
    public void Should_Have_Options()
    {
        var command = new DownloadLogsCommand();
        Assert.NotNull(command);
        Assert.Equal("download-logs", command.Name);
        Assert.Equal(8, command.Options.Count);

        TestHelpers.VerifyCommandOption(command.Options, "github-target-org", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-repo", false);
        TestHelpers.VerifyCommandOption(command.Options, "migration-id", false);
        TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(command.Options, "migration-log-file", false);
        TestHelpers.VerifyCommandOption(command.Options, "overwrite", false);
        TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
    }
}
