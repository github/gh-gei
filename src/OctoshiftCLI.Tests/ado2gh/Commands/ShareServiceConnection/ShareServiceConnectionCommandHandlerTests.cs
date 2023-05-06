using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.ShareServiceConnection;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.ShareServiceConnection;

public class ShareServiceConnectionCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ShareServiceConnectionCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private readonly string SERVICE_CONNECTION_ID = Guid.NewGuid().ToString();
    private readonly string TEAM_PROJECT_ID = Guid.NewGuid().ToString();

    public ShareServiceConnectionCommandHandlerTests()
    {
        _handler = new ShareServiceConnectionCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.ContainsServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, SERVICE_CONNECTION_ID).Result).Returns(false);

        var args = new ShareServiceConnectionCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.ShareServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, SERVICE_CONNECTION_ID));
    }

    [Fact]
    public async Task It_Skips_When_Already_Shared()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.ContainsServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, SERVICE_CONNECTION_ID).Result).Returns(true);

        var args = new ShareServiceConnectionCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.ShareServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, TEAM_PROJECT_ID, SERVICE_CONNECTION_ID), Times.Never);
    }
}
