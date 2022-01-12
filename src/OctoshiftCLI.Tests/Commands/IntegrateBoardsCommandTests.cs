using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new IntegrateBoardsCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task No_Existing_Connection()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizationId(userId, adoOrg).Result).Returns(orgId);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, orgId, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, orgId, adoTeamProject).Result).Returns(() => default);
            mockAdo.Setup(x => x.CreateBoardsGithubEndpoint(adoOrg, teamProjectId, githubToken, githubHandle, It.IsAny<string>()).Result).Returns(endpointId);
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, orgId, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);

            var command = new IntegrateBoardsCommand(new Mock<OctoLogger>().Object, adoFactory,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubConnection(adoOrg, orgId, adoTeamProject, endpointId, newRepoId));
        }

        [Fact]
        public async Task Add_Repo_To_Existing_Connection()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "foo-connection";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", "34" };
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizationId(userId, adoOrg).Result).Returns(orgId);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, orgId, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, orgId, adoTeamProject).Result).Returns((connectionId, endpointId, connectionName, repoIds));
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, orgId, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);

            var command = new IntegrateBoardsCommand(new Mock<OctoLogger>().Object, adoFactory,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockAdo.Verify(x => x.AddRepoToBoardsGithubConnection(adoOrg, orgId, adoTeamProject, connectionId, connectionName, endpointId, Moq.It.Is<IEnumerable<string>>(x => x.Contains(repoIds[0]) &&
                                                                                                                                                                               x.Contains(repoIds[1]) &&
                                                                                                                                                                               x.Contains(newRepoId))));
        }

        [Fact]
        public async Task Repo_Already_Integrated()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "foo-connection";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", newRepoId, "34" };
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetUserId().Result).Returns(userId);
            mockAdo.Setup(x => x.GetOrganizationId(userId, adoOrg).Result).Returns(orgId);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, orgId, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, orgId, adoTeamProject).Result).Returns((connectionId, endpointId, connectionName, repoIds));
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, orgId, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);

            var command = new IntegrateBoardsCommand(new Mock<OctoLogger>().Object, adoFactory,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockAdo.Verify(x => x.AddRepoToBoardsGithubConnection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }
    }
}