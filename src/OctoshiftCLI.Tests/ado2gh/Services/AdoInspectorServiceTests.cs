using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class AdoInspectorServiceTests
    {
        private readonly OctoLogger _logger = TestHelpers.CreateMock<OctoLogger>().Object;
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly AdoInspectorService _service;

        private const string ADO_ORG = "ADO_ORG";
        private const string ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT";
        private const string FOO_REPO = "FOO_REPO";
        private readonly IEnumerable<string> ADO_REPOS = new List<string>() { FOO_REPO };

        public AdoInspectorServiceTests() => _service = new(_logger);

        [Fact]
        public async Task GetOrgs_Should_Return_All_Orgs()
        {
            // Arrange
            var org1 = "my-org";
            var org2 = "other-org";
            var orgs = new List<string>() { org1, org2 };
            var userId = Guid.NewGuid().ToString();

            _mockAdoApi.Setup(m => m.GetUserId()).ReturnsAsync(userId);
            _mockAdoApi.Setup(m => m.GetOrganizations(userId)).ReturnsAsync(orgs);

            _service.AdoApi = _mockAdoApi.Object;

            // Act
            var result = await _service.GetOrgs();

            // Assert
            result.Should().BeEquivalentTo(orgs);
        }

        [Fact]
        public async Task GetOrgs_Should_Return_Single_Org_Collection_When_Org_Passed()
        {
            // Arrange
            _service.OrgFilter = ADO_ORG;
            _service.AdoApi = _mockAdoApi.Object;

            // Act
            var result = await _service.GetOrgs();

            // Assert
            result.Count().Should().Be(1);
            result.First().Should().Be(ADO_ORG);

            _mockAdoApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetOrgs_Should_Throw_Exception_When_AdoApi_Not_Set()
        {
            await FluentActions
                .Invoking(async () => await _service.GetOrgs())
                .Should()
                .ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetTeamProjects_Should_Return_All_TeamProjects()
        {
            // Arrange
            var teamProject1 = "foo";
            var teamProject2 = "bar";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };

            _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(teamProjects);
            _service.AdoApi = _mockAdoApi.Object;

            // Act
            var result = await _service.GetTeamProjects(ADO_ORG);

            // Assert
            result.Should().BeEquivalentTo(teamProjects);
        }

        [Fact]
        public async Task GetTeamProjects_Should_Return_Single_TeamProject_When_TeamProjectFilter_Set()
        {
            _service.AdoApi = _mockAdoApi.Object;
            _service.TeamProjectFilter = ADO_TEAM_PROJECT;

            var result = await _service.GetTeamProjects(ADO_ORG);

            result.Count().Should().Be(1);
            result.First().Should().Be(ADO_TEAM_PROJECT);
        }

        [Fact]
        public async Task GetTeamProjects_Should_Throw_Exception_When_AdoApi_Not_Set()
        {
            await FluentActions
                .Invoking(async () => await _service.GetTeamProjects(ADO_ORG))
                .Should()
                .ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetRepos_Should_Return_All_Repos()
        {
            // Arrange
            var repo1 = "foo";
            var repo2 = "bar";
            var repos = new List<string>() { repo1, repo2 };

            _mockAdoApi.Setup(m => m.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT)).ReturnsAsync(repos);
            _service.AdoApi = _mockAdoApi.Object;

            // Act
            var result = await _service.GetRepos(ADO_ORG, ADO_TEAM_PROJECT);

            // Assert
            result.Should().BeEquivalentTo(repos);
        }

        [Fact]
        public async Task GetRepos_Should_Throw_Exception_When_AdoApi_Not_Set()
        {
            await FluentActions
                .Invoking(async () => await _service.GetRepos(ADO_ORG, ADO_TEAM_PROJECT))
                .Should()
                .ThrowAsync<Exception>();
        }

        [Fact]
        public async Task GetPipelines_Should_Return_All_Pipelines()
        {
            // Arrange
            var repoId = Guid.NewGuid().ToString();
            var pipeline1 = "foo";
            var pipeline2 = "bar";
            var pipelines = new List<string>() { pipeline1, pipeline2 };

            _mockAdoApi.Setup(m => m.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO)).ReturnsAsync(repoId);
            _mockAdoApi.Setup(m => m.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, repoId)).ReturnsAsync(pipelines);

            _service.AdoApi = _mockAdoApi.Object;

            // Act
            var result = await _service.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO);

            // Assert
            result.Should().BeEquivalentTo(pipelines);
        }

        [Fact]
        public async Task GetPipelines_Should_Throw_Exception_When_AdoApi_Not_Set()
        {
            await FluentActions
                .Invoking(async () => await _service.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO))
                .Should()
                .ThrowAsync<Exception>();
        }
    }
}
