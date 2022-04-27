using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class IntegrateBoardsCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new IntegrateBoardsCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("integrate-boards", command.Name);
            Assert.Equal(7, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task No_Existing_Connection()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, adoTeamProject).Result).Returns(() => default);
            mockAdo.Setup(x => x.CreateBoardsGithubEndpoint(adoOrg, teamProjectId, githubToken, githubHandle, It.IsAny<string>()).Result).Returns(endpointId);
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = TestHelpers.CreateMock<OctoshiftCLI.AdoToGithub.EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdo.Object);

            var command = new IntegrateBoardsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubConnection(adoOrg, adoTeamProject, endpointId, newRepoId));
        }

        [Fact]
        public async Task Add_Repo_To_Existing_Connection()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "foo-connection";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", "34" };
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, adoTeamProject).Result).Returns((connectionId, endpointId, connectionName, repoIds));
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = TestHelpers.CreateMock<OctoshiftCLI.AdoToGithub.EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdo.Object);

            var command = new IntegrateBoardsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockAdo.Verify(x => x.AddRepoToBoardsGithubConnection(adoOrg, adoTeamProject, connectionId, connectionName, endpointId, Moq.It.Is<IEnumerable<string>>(x => x.Contains(repoIds[0]) &&
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
            var teamProjectId = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "foo-connection";
            var endpointId = Guid.NewGuid().ToString();
            var newRepoId = Guid.NewGuid().ToString();
            var repoIds = new List<string>() { "12", newRepoId, "34" };
            var githubToken = Guid.NewGuid().ToString();

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetGithubHandle(adoOrg, adoTeamProject, githubToken).Result).Returns(githubHandle);
            mockAdo.Setup(x => x.GetBoardsGithubConnection(adoOrg, adoTeamProject).Result).Returns((connectionId, endpointId, connectionName, repoIds));
            mockAdo.Setup(x => x.GetBoardsGithubRepoId(adoOrg, adoTeamProject, teamProjectId, endpointId, githubOrg, githubRepo).Result).Returns(newRepoId);

            var environmentVariableProviderMock = TestHelpers.CreateMock<OctoshiftCLI.AdoToGithub.EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubToken);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(null)).Returns(mockAdo.Object);

            var command = new IntegrateBoardsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke(adoOrg, adoTeamProject, githubOrg, githubRepo);

            mockAdo.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            mockAdo.Verify(x => x.AddRepoToBoardsGithubConnection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_And_Github_Pats_When_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new IntegrateBoardsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "githubOrg", "githubRepo", adoPat, githubPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public async Task It_Falls_Back_To_Github_Pat_From_Environment_When_Not_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            var environmentVariableProviderMock = TestHelpers.CreateMock<EnvironmentVariableProvider>();
            environmentVariableProviderMock
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new IntegrateBoardsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object,
                environmentVariableProviderMock.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "githubOrg", "githubRepo", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
            environmentVariableProviderMock.Verify(m => m.GithubPersonalAccessToken(), Times.Once);
        }
    }
}
