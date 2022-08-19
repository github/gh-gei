using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands;

public class CreateTeamCommandTests
{
    private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly CreateTeamCommand _command;

    public CreateTeamCommandTests()
    {
        _command = new CreateTeamCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
    }

    [Fact]
    public void Should_Have_Options()
    {
        Assert.NotNull(_command);
        Assert.Equal("create-team", _command.Name);
        Assert.Equal(5, _command.Options.Count);

        TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
        TestHelpers.VerifyCommandOption(_command.Options, "team-name", true);
        TestHelpers.VerifyCommandOption(_command.Options, "idp-group", false);
        TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
        TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
    }
}
