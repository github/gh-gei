using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.InventoryReport;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.InventoryReport;

public class InventoryReportCommandArgsTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private const string ADO_ORG = "ado-org";
    private const string ADO_TEAM_PROJECT = "ado-project";

    [Fact]
    public void Validate_Throws_When_AdoTeamProject_Is_Specified_Without_AdoOrg()
    {
        var args = new InventoryReportCommandArgs
        {
            AdoTeamProject = ADO_TEAM_PROJECT
        };

        FluentActions.Invoking(() => args.Validate(_mockOctoLogger.Object))
            .Should()
            .ThrowExactly<OctoshiftCliException>()
            .WithMessage("The --ado-team-project option requires the --ado-org option to also be provided.");
    }

    [Fact]
    public void Validate_Succeeds_With_Valid_Names()
    {
        var args = new InventoryReportCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
        };

        args.Validate(_mockOctoLogger.Object);

        args.AdoOrg.Should().Be(ADO_ORG);
        args.AdoTeamProject.Should().Be(ADO_TEAM_PROJECT);
    }
}
