using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class DownloadLogsCommandTests
{
    private readonly DownloadLogsCommand _command;
    private readonly Mock<ITargetGithubApiFactory> _targetGithubApiFactory = new();
    private readonly Mock<HttpDownloadService> _mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
    private readonly Mock<OctoLogger> _mockLogger = TestHelpers.CreateMock<OctoLogger>();

    public DownloadLogsCommandTests()
    {
        _command = new DownloadLogsCommand(
            _mockLogger.Object,
            _targetGithubApiFactory.Object,
            _mockHttpDownloadService.Object,
            new RetryPolicy(_mockLogger.Object) { _retryOnResultInterval = 0 });
    }

    [Fact]
    public void Should_Have_Overridden_Options()
    {
        Assert.NotNull(_command);
        Assert.Equal("download-logs", _command.Name);
        Assert.Equal(7, _command.Options.Count);

        TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-repo", true);
        TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "migration-log-file", false);
        TestHelpers.VerifyCommandOption(_command.Options, "overwrite", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
