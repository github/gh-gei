//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using FluentAssertions;
//using Moq;
//using OctoshiftCLI.AdoToGithub;
//using Xunit;

//namespace OctoshiftCLI.Tests.AdoToGithub.Commands
//{
//    public class AdoInspectorServiceTests
//    {
//        private readonly OctoLogger _logger = TestHelpers.CreateMock<OctoLogger>().Object;
//        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
//        private readonly AdoInspectorService _service;

//        private const string ADO_ORG = "ADO_ORG";
//        private const string ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT";
//        private const string FOO_REPO = "FOO_REPO";

//        private readonly IEnumerable<string> ADO_ORGS = new List<string>() { ADO_ORG };
//        private readonly IDictionary<string, IEnumerable<string>> ADO_TEAM_PROJECTS = new Dictionary<string, IEnumerable<string>>() { { ADO_ORG, new List<string>() { ADO_TEAM_PROJECT } } };
//        private readonly IDictionary<string, IDictionary<string, IEnumerable<string>>> ADO_REPOS = new Dictionary<string, IDictionary<string, IEnumerable<string>>>() { { ADO_ORG, new Dictionary<string, IEnumerable<string>>() { { ADO_TEAM_PROJECT, new List<string>() { FOO_REPO } } } } };

//        public AdoInspectorServiceTests() => _service = new(_logger);

//        [Fact]
//        public async Task GetOrgs_Should_Return_All_Orgs()
//        {
//            // Arrange
//            var org1 = "my-org";
//            var org2 = "other-org";
//            var orgs = new List<string>() { org1, org2 };
//            var userId = Guid.NewGuid().ToString();

//            _mockAdoApi.Setup(m => m.GetUserId()).ReturnsAsync(userId);
//            _mockAdoApi.Setup(m => m.GetOrganizations(userId)).ReturnsAsync(orgs);

//            // Act
//            var result = await _service.GetOrgs(_mockAdoApi.Object);

//            // Assert
//            result.Should().BeEquivalentTo(orgs);
//        }

//        [Fact]
//        public async Task GetOrgs_Should_Return_Single_Org_Collection_When_Org_Passed()
//        {
//            // Arrange
//            var org1 = "my-org";

//            // Act
//            var result = await _service.GetOrgs(null, org1);

//            // Assert
//            result.Count().Should().Be(1);
//            result.First().Should().Be(org1);

//            _mockAdoApi.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public async Task GetOrgs_Should_Return_Empty_List_When_Null_AdoApi_Passed()
//        {
//            var result = await _service.GetOrgs(null);
//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Return_All_TeamProjects_With_One_Org()
//        {
//            // Arrange
//            var teamProject1 = "foo";
//            var teamProject2 = "bar";
//            var teamProjects = new List<string>() { teamProject1, teamProject2 };

//            _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(teamProjects);

//            // Act
//            var result = await _service.GetTeamProjects(_mockAdoApi.Object, ADO_ORGS);

//            // Assert
//            result.Should().HaveCount(1);
//            result.First().Key.Should().Be(ADO_ORG);
//            result[ADO_ORG].Should().BeEquivalentTo(teamProjects);
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Return_All_TeamProjects_With_Multiple_Orgs()
//        {
//            // Arrange
//            var org1 = "my-org";
//            var org2 = "some-org";
//            var orgs = new List<string>() { org1, org2 };
//            var teamProject1 = "foo";
//            var teamProject2 = "bar";
//            var teamProjects1 = new List<string>() { teamProject1, teamProject2 };
//            var teamProject3 = "sales";
//            var teamProject4 = "shipping";
//            var teamProjects2 = new List<string>() { teamProject3, teamProject4 };

//            _mockAdoApi.Setup(m => m.GetTeamProjects(org1)).ReturnsAsync(teamProjects1);
//            _mockAdoApi.Setup(m => m.GetTeamProjects(org2)).ReturnsAsync(teamProjects2);

//            // Act
//            var result = await _service.GetTeamProjects(_mockAdoApi.Object, orgs);

//            // Assert
//            result.Should().HaveCount(2);
//            result[org1].Should().BeEquivalentTo(teamProjects1);
//            result[org2].Should().BeEquivalentTo(teamProjects2);
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Return_Single_TeamProject_When_TeamProject_Passed()
//        {
//            var result = await _service.GetTeamProjects(null, ADO_ORGS, ADO_TEAM_PROJECT);
//            result[ADO_ORG].Single().Should().Be(ADO_TEAM_PROJECT);
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Throw_Exception_When_Multiple_Orgs_But_One_TeamProject_Passed()
//        {
//            // Arrange
//            var org1 = "my-org";
//            var org2 = "other-org";
//            var orgs = new List<string>() { org1, org2 };

//            // Act
//            await FluentActions
//                .Invoking(async () => await _service.GetTeamProjects(null, orgs, ADO_TEAM_PROJECT))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Throw_Exception_When_Empty_Orgs_But_One_TeamProject_Passed()
//        {
//            var orgs = new List<string>();

//            await FluentActions
//                .Invoking(async () => await _service.GetTeamProjects(null, orgs, ADO_TEAM_PROJECT))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Return_Empty_When_Null_AdoApi_Passed()
//        {
//            var result = await _service.GetTeamProjects(null, ADO_ORGS, null);

//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetTeamProjects_Should_Return_Empty_When_Null_Orgs_Passed()
//        {
//            var result = await _service.GetTeamProjects(_mockAdoApi.Object, null);

//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Return_All_Repos()
//        {
//            // Arrange
//            var repo1 = "foo";
//            var repo2 = "bar";
//            var repos = new List<string>() { repo1, repo2 };

//            _mockAdoApi.Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(repos);

//            // Act
//            var result = await _service.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS);

//            // Assert
//            result[ADO_ORG][ADO_TEAM_PROJECT].Should().BeEquivalentTo(repos);
//        }

//        [Fact]
//        public async Task GetRepos_Should_Return_One_Repo_When_Repo_Passed()
//        {
//            var result = await _service.GetRepos(null, ADO_TEAM_PROJECTS, FOO_REPO);
//            result[ADO_ORG][ADO_TEAM_PROJECT].Single().Should().Be(FOO_REPO);
//        }

//        [Fact]
//        public async Task GetRepos_Should_Return_Empty_When_AdoApi_Is_Null()
//        {
//            var result = await _service.GetRepos(null, ADO_TEAM_PROJECTS);
//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Return_Empty_When_TeamProjects_Is_Null()
//        {
//            var result = await _service.GetRepos(_mockAdoApi.Object, null);
//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Throw_Exception_When_TeamProject_Provided_And_TeamProjects_Is_Null()
//        {
//            await FluentActions
//                .Invoking(async () => await _service.GetRepos(_mockAdoApi.Object, null, FOO_REPO))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Throw_Exception_When_TeamProject_Provided_And_TeamProjects_Is_Empty()
//        {
//            var emptyTeamProjects = new Dictionary<string, IEnumerable<string>>();

//            await FluentActions
//                .Invoking(async () => await _service.GetRepos(_mockAdoApi.Object, emptyTeamProjects, FOO_REPO))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Throw_Exception_When_TeamProject_Provided_And_TeamProjects_Has_More_Than_One_Org()
//        {
//            ADO_TEAM_PROJECTS.Add("blah", new List<string>());

//            await FluentActions
//                .Invoking(async () => await _service.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, FOO_REPO))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Throw_Exception_When_TeamProject_Provided_And_TeamProjects_Has_More_Than_One_TeamProject()
//        {
//            ADO_TEAM_PROJECTS[ADO_ORG] = new List<string>() { "foo", "bar" };

//            await FluentActions
//                .Invoking(async () => await _service.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, FOO_REPO))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetRepos_Should_Throw_Exception_When_TeamProject_Provided_And_TeamProjects_Has_An_Org_But_No_TeamProjects()
//        {
//            ADO_TEAM_PROJECTS[ADO_ORG] = new List<string>();

//            await FluentActions
//                .Invoking(async () => await _service.GetRepos(_mockAdoApi.Object, ADO_TEAM_PROJECTS, FOO_REPO))
//                .Should()
//                .ThrowExactlyAsync<ArgumentException>();
//        }

//        [Fact]
//        public async Task GetPipelines_Should_Return_All_Pipelines()
//        {
//            // Arrange
//            var repoId = Guid.NewGuid().ToString();
//            var pipeline1 = "foo";
//            var pipeline2 = "bar";
//            var pipelines = new List<string>() { pipeline1, pipeline2 };

//            _mockAdoApi.Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(repoId);
//            _mockAdoApi.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, repoId)).ReturnsAsync(pipelines);

//            // Act
//            var result = await _service.GetPipelines(_mockAdoApi.Object, ADO_REPOS);

//            // Assert
//            result[ADO_ORG][ADO_TEAM_PROJECT][FOO_REPO].Should().BeEquivalentTo(pipelines);
//        }

//        [Fact]
//        public async Task GetPipelines_Should_Return_Empty_When_AdoApi_Is_Null()
//        {
//            var result = await _service.GetPipelines(null, ADO_REPOS);
//            result.Should().BeEmpty();
//        }

//        [Fact]
//        public async Task GetPipelines_Should_Return_Empty_When_Repos_Is_Null()
//        {
//            var result = await _service.GetPipelines(_mockAdoApi.Object, null);
//            result.Should().BeEmpty();
//        }
//    }
//}
