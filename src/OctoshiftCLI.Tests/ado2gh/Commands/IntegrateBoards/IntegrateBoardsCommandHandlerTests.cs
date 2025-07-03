using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.IntegrateBoards;

public class IntegrateBoardsCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly IntegrateBoardsCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string GITHUB_ORG = "foo-gh-org";
    private const string GITHUB_REPO = "foo-repo";
    private readonly string TEAM_PROJECT_ID = Guid.NewGuid().ToString();
    private readonly string CONNECTION_ID = Guid.NewGuid().ToString();
    private const string CONNECTION_NAME = "foo-connection";
    private readonly string ENDPOINT_ID = Guid.NewGuid().ToString();
    private readonly string NEW_REPO_ID = Guid.NewGuid().ToString();
    private readonly string SERVICE_CONNECTION_ID = Guid.NewGuid().ToString();

    public IntegrateBoardsCommandHandlerTests()
    {
        _handler = new IntegrateBoardsCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object);
    }

    [Fact]
    public async Task No_Existing_Connection_With_Service_Connection_Id()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(() => default);
        _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, SERVICE_CONNECTION_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

        var args = new IntegrateBoardsCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, SERVICE_CONNECTION_ID, NEW_REPO_ID));
    }

    [Fact]
    public async Task No_Existing_Connection_Auto_Find_Service_Connection()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubAppServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_ORG).Result).Returns(SERVICE_CONNECTION_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(() => default);
        _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, SERVICE_CONNECTION_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

        var args = new IntegrateBoardsCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, SERVICE_CONNECTION_ID, NEW_REPO_ID));
    }

    [Fact]
    public async Task Add_Repo_To_Existing_Connection()
    {
        var repoIds = new List<string>() { "12", "34" };

        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns((CONNECTION_ID, ENDPOINT_ID, CONNECTION_NAME, repoIds));
        _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, ENDPOINT_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

        var args = new IntegrateBoardsCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.AddRepoToBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, CONNECTION_ID, CONNECTION_NAME, ENDPOINT_ID, It.Is<IEnumerable<string>>(x => x.Contains(repoIds[0]) &&
                                                                                                                                                                          x.Contains(repoIds[1]) &&
                                                                                                                                                                          x.Contains(NEW_REPO_ID))));
    }

    [Fact]
    public async Task Repo_Already_Integrated()
    {
        var repoIds = new List<string>() { "12", NEW_REPO_ID, "34" };

        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns((CONNECTION_ID, ENDPOINT_ID, CONNECTION_NAME, repoIds));
        _mockAdoApi.Setup(x => x.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, ENDPOINT_ID, GITHUB_ORG, GITHUB_REPO).Result).Returns(NEW_REPO_ID);

        var args = new IntegrateBoardsCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.AddRepoToBoardsGithubConnection(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task Throws_Exception_When_No_Service_Connection_Found()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetBoardsGithubAppServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, GITHUB_ORG).Result).Returns((string)null);

        var args = new IntegrateBoardsCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
        };

        var exception = await Assert.ThrowsAsync<OctoshiftCliException>(() => _handler.Handle(args));
        Assert.Contains("No GitHub App service connection found", exception.Message);
    }
}
