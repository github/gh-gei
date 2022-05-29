//using System.Collections.Generic;
//using System.Threading.Tasks;
//using FluentAssertions;
//using Moq;
//using OctoshiftCLI.AdoToGithub;
//using OctoshiftCLI.AdoToGithub.Commands;
//using Xunit;

//namespace OctoshiftCLI.Tests.AdoToGithub.Commands
//{
//    public class InventoryReportCommandTests
//    {
//        private const string ADO_ORG = "foo-org";
//        private const string ADO_TEAM_PROJECT = "foo-tp";
//        private const string FOO_REPO = "foo-repo";
//        private const string FOO_PIPELINE = "foo-pipeline";
//        private readonly IList<string> ADO_ORGS = new List<string>() { ADO_ORG };
//        private readonly IDictionary<string, IEnumerable<string>> ADO_TEAM_PROJECTS = new Dictionary<string, IEnumerable<string>>() { { ADO_ORG, new List<string>() { ADO_TEAM_PROJECT } } };
//        private readonly IDictionary<string, IDictionary<string, IEnumerable<string>>> ADO_REPOS = new Dictionary<string, IDictionary<string, IEnumerable<string>>>() { { ADO_ORG, new Dictionary<string, IEnumerable<string>>() { { ADO_TEAM_PROJECT, new List<string>() { FOO_REPO } } } } };
//        private readonly IDictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>> ADO_PIPELINES =
//    new Dictionary<string, IDictionary<string, IDictionary<string, IEnumerable<string>>>>()
//    { { ADO_ORG, new Dictionary<string, IDictionary<string, IEnumerable<string>>>()
//                         { { ADO_TEAM_PROJECT, new Dictionary<string, IEnumerable<string>>()
//                                               { { FOO_REPO, new List<string>()
//                                                             { FOO_PIPELINE } } } } } } };

//        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
//        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
//        private readonly Mock<AdoInspectorService> _mockAdoInspector = TestHelpers.CreateMock<AdoInspectorService>();
//        private readonly Mock<OrgsCsvGeneratorService> _mockOrgsCsvGenerator = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
//        private readonly Mock<TeamProjectsCsvGeneratorService> _mockTeamProjectsCsvGenerator = TestHelpers.CreateMock<TeamProjectsCsvGeneratorService>();
//        private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGenerator = TestHelpers.CreateMock<ReposCsvGeneratorService>();
//        private readonly Mock<PipelinesCsvGeneratorService> _mockPipelinesCsvGenerator = TestHelpers.CreateMock<PipelinesCsvGeneratorService>();

//        private string _orgsCsvOutput = "";
//        private string _teamProjectsCsvOutput = "";
//        private string _reposCsvOutput = "";
//        private string _pipelinesCsvOutput = "";

//        private readonly InventoryReportCommand _command;

//        public InventoryReportCommandTests()
//        {
//            _command = new InventoryReportCommand(TestHelpers.CreateMock<OctoLogger>().Object, _mockAdoApiFactory.Object, _mockAdoInspector.Object, _mockOrgsCsvGenerator.Object, _mockTeamProjectsCsvGenerator.Object, _mockReposCsvGenerator.Object, _mockPipelinesCsvGenerator.Object)
//            {
//                WriteToFile = (path, contents) =>
//                {
//                    if (path == "orgs.csv")
//                    {
//                        _orgsCsvOutput = contents;
//                    }

//                    if (path == "team-projects.csv")
//                    {
//                        _teamProjectsCsvOutput = contents;
//                    }

//                    if (path == "repos.csv")
//                    {
//                        _reposCsvOutput = contents;
//                    }

//                    if (path == "pipelines.csv")
//                    {
//                        _pipelinesCsvOutput = contents;
//                    }

//                    return Task.CompletedTask;
//                }
//            };
//        }

//        [Fact]
//        public void Should_Have_Options()
//        {
//            Assert.NotNull(_command);
//            Assert.Equal("inventory-report", _command.Name);
//            Assert.Equal(3, _command.Options.Count);

//            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", false);
//            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
//            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
//        }

//        [Fact]
//        public async Task Happy_Path()
//        {
//            var expectedOrgsCsv = "csv stuff";
//            var expectedTeamProjectsCsv = "more csv stuff";
//            var expectedReposCsv = "repo csv stuff";
//            var expectedPipelinesCsv = "pipelines csv stuff";

//            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

//            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, null)).ReturnsAsync(ADO_ORGS);
//            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
//            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
//            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

//            _mockOrgsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedOrgsCsv);
//            _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(ADO_PIPELINES)).Returns(expectedTeamProjectsCsv);
//            _mockReposCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedReposCsv);
//            _mockPipelinesCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedPipelinesCsv);

//            await _command.Invoke(null, null);

//            _orgsCsvOutput.Should().Be(expectedOrgsCsv);
//            _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
//            _reposCsvOutput.Should().Be(expectedReposCsv);
//            _pipelinesCsvOutput.Should().Be(expectedPipelinesCsv);
//        }

//        [Fact]
//        public async Task Scoped_To_Single_Org()
//        {
//            var expectedOrgsCsv = "csv stuff";
//            var expectedTeamProjectsCsv = "more csv stuff";
//            var expectedReposCsv = "repo csv stuff";
//            var expectedPipelinesCsv = "pipelines csv stuff";

//            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

//            _mockAdoInspector.Setup(m => m.GetOrgs(_mockAdoApi.Object, ADO_ORG)).ReturnsAsync(ADO_ORGS);
//            _mockAdoInspector.Setup(m => m.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS, null)).ReturnsAsync(ADO_TEAM_PROJECTS);
//            _mockAdoInspector.Setup(m => m.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, null)).ReturnsAsync(ADO_REPOS);
//            _mockAdoInspector.Setup(m => m.GetPipelines(_mockAdoApi.Object, ADO_REPOS)).ReturnsAsync(ADO_PIPELINES);

//            _mockOrgsCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedOrgsCsv);
//            _mockTeamProjectsCsvGenerator.Setup(m => m.Generate(ADO_PIPELINES)).Returns(expectedTeamProjectsCsv);
//            _mockReposCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedReposCsv);
//            _mockPipelinesCsvGenerator.Setup(m => m.Generate(_mockAdoApi.Object, ADO_PIPELINES)).ReturnsAsync(expectedPipelinesCsv);

//            await _command.Invoke(ADO_ORG, null);

//            _orgsCsvOutput.Should().Be(expectedOrgsCsv);
//            _teamProjectsCsvOutput.Should().Be(expectedTeamProjectsCsv);
//            _reposCsvOutput.Should().Be(expectedReposCsv);
//            _pipelinesCsvOutput.Should().Be(expectedPipelinesCsv);
//        }

//        [Fact]
//        public async Task It_Uses_The_Ado_Pat_When_Provided()
//        {
//            const string adoPat = "ado-pat";

//            await _command.Invoke("some org", adoPat);

//            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
//        }
//    }
//}
