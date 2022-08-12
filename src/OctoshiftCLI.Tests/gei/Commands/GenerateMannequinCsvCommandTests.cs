using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class GenerateMannequinCsvCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly GenerateMannequinCsvCommand _command;

    public GenerateMannequinCsvCommandTests()
    {
        _command = new GenerateMannequinCsvCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        Assert.NotNull(_command);
        Assert.Equal("generate-mannequin-csv", _command.Name);
        Assert.Equal(5, _command.Options.Count);

        TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "output", false);
        TestHelpers.VerifyCommandOption(_command.Options, "include-reclaimed", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
