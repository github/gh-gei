using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class InventoryReportCommandTests
    {
        private const string ADO_ORG = "foo-org";
        private const string ADO_TEAM_PROJECT = "foo-tp";
        private readonly IList<string> ADO_ORGS = new List<string>() { ADO_ORG };
        private readonly IDictionary<string, IEnumerable<string>> ADO_TEAM_PROJECTS = new Dictionary<string, IEnumerable<string>>() { { ADO_ORG, new List<string>() { ADO_TEAM_PROJECT } } };

        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorService> _mockAdoInspector = TestHelpers.CreateMock<AdoInspectorService>();
        private readonly Mock<OrgsCsvGeneratorService> _mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
        private readonly Mock<TeamProjectsCsvGeneratorService> _mockTeamProjectsCsvGenerator = TestHelpers.CreateMock<TeamProjectsCsvGeneratorService>();

        private string _orgsCsvOutput = "";
        private string _teamProjectsCsvOutput = "";

        private readonly InventoryReportCommand _command;

        public InventoryReportCommandTests()
        {
            _command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, _mockAdoApiFactory.Object, _mockAdoInspector.Object, _mockOrgsCsvGenerator.Object, _mockTeamProjectsCsvGenerator.Object)
            {
                WriteToFile = (path, contents) =>
                {
                    if (path == "orgs.csv")
                    {
                        _orgsCsvOutput = contents;
                    }

                    if (path == "team-projects.csv")
                    {
                        _teamProjectsCsvOutput = contents;
                    }

                    return Task.CompletedTask;
                }
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new InventoryReportCommand(null, null, null, null, null);

            Assert.NotNull(command);
            Assert.Equal("inventory-report", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var expectedOrgsCsv = "csv stuff";
            var expectedTeamProjectsCsv = "more csv stuff";

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, null)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);

            _mockOrgsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_ORGS)).ReturnsAsync(expectedOrgsCsv);
            _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_TEAM_PROJECTS)).Returns(expectedTeamProjectsCsv);

            await _command.Invoke(null, null);

            _orgsCsvOutput.Should().Be(expectedOrgsCsv);
            _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
        }

        [Fact]
        public async Task Scoped_To_Single_Org()
        {
            var expectedOrgsCsv = "csv stuff";
            var expectedTeamProjectsCsv = "more csv stuff";

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);

            _mockOrgsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_ORGS)).ReturnsAsync(expectedOrgsCsv);

            _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_TEAM_PROJECTS)).Returns(expectedTeamProjectsCsv);

            await _command.Invoke(ADO_ORG, null);

            _orgsCsvOutput.Should().Be(expectedOrgsCsv);
            _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, _mockAdoApiFactory.Object, _mockAdoInspector.Object, _mockOrgsCsvGenerator.Object, _mockTeamProjectsCsvGenerator.Object);
            await command.Invoke("some org", adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
