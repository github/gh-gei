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
        private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly IntegrateBoardsCommand _command;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string GITHUB_ORG = "foo-gh-org";
        private const string GITHUB_REPO = "foo-repo";
        private readonly string TEAM_PROJECT_ID = Guid.NewGuid().ToString();
        private const string GITHUB_HANDLE = "foo-handle";
        private readonly string CONNECTION_ID = Guid.NewGuid().ToString();
        private const string CONNECTION_NAME = "foo-connection";
        private readonly string ENDPOINT_ID = Guid.NewGuid().ToString();
        private readonly string NEW_REPO_ID = Guid.NewGuid().ToString();
        private readonly string GITHUB_TOKEN = Guid.NewGuid().ToString();

        public IntegrateBoardsCommandTests()
        {
            _command = new IntegrateBoardsCommand(_mockOctoLogger.Object, _mockAdoApiFactory.Object, _mockEnvironmentVariableProvider.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("integrate-boards", _command.Name);
            Assert.Equal(7, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task No_Existing_Connection()
        {
            _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
            _mockAdoApi.Setup(x => x.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_TOKEN).Result).Returns(GITHUB_HANDLE);
            _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(() => default);
            _mockAdoApi.Setup(x => x.CreateBoardsGithubEndpoint(ADO_ORG, TEAM_PROJECT_ID, GITHUB_TOKEN, GITHUB_HANDLE, It.IsAny<string>()).Result).Returns(ENDPOINT_ID);
            _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, ENDPOINT_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_ORG, GITHUB_REPO);

            _mockAdoApi.Verify(x => x.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, ENDPOINT_ID, NEW_REPO_ID));
        }

        [Fact]
        public async Task Add_Repo_To_Existing_Connection()
        {
            var repoIds = new List<string>() { "12", "34" };

            _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
            _mockAdoApi.Setup(x => x.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_TOKEN).Result).Returns(GITHUB_HANDLE);
            _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns((CONNECTION_ID, ENDPOINT_ID, CONNECTION_NAME, repoIds));
            _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, ENDPOINT_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_ORG, GITHUB_REPO);

            _mockAdoApi.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockAdoApi.Verify(x => x.AddRepoToBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, CONNECTION_ID, CONNECTION_NAME, ENDPOINT_ID, Moq.It.Is<IEnumerable<string>>(x => x.Contains(repoIds[0]) &&
                                                                                                                                                                               x.Contains(repoIds[1]) &&
                                                                                                                                                                               x.Contains(NEW_REPO_ID))));
        }

        [Fact]
        public async Task Repo_Already_Integrated()
        {
            var repoIds = new List<string>() { "12", NEW_REPO_ID, "34" };

            _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
            _mockAdoApi.Setup(x => x.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_TOKEN).Result).Returns(GITHUB_HANDLE);
            _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns((CONNECTION_ID, ENDPOINT_ID, CONNECTION_NAME, repoIds));
            _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, ENDPOINT_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);

            _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

            await _command.Invoke(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_ORG, GITHUB_REPO);

            _mockAdoApi.Verify(x => x.CreateBoardsGithubEndpoint(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockAdoApi.Verify(x => x.AddRepoToBoardsGithubConnection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_And_Github_Pats_When_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "githubOrg", "githubRepo", adoPat, githubPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
            _mockEnvironmentVariableProvider.Verify(m => m.GithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public async Task It_Falls_Back_To_Github_Pat_From_Environment_When_Not_Provided()
        {
            const string adoPat = "ado-pat";
            const string githubPat = "github-pat";

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(githubPat);

            _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

            await _command.Invoke("adoOrg", "adoTeamProject", "githubOrg", "githubRepo", adoPat);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
            _mockEnvironmentVariableProvider.Verify(m => m.GithubPersonalAccessToken(), Times.Once);
        }
    }
}
