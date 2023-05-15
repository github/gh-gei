using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.LockRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.LockRepo;

public class LockRepoCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly LockRepoCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string ADO_REPO = "foo-repo";
    private readonly string REPO_ID = Guid.NewGuid().ToString();
    private const string IDENTITY_DESCRIPTOR = "foo-id";
    private readonly string TEAM_PROJECT_ID = Guid.NewGuid().ToString();

    public LockRepoCommandHandlerTests()
    {
        _handler = new LockRepoCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        _mockAdoApi.Setup(x => x.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(TEAM_PROJECT_ID);
        _mockAdoApi.Setup(x => x.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO).Result).Returns(REPO_ID);
        _mockAdoApi.Setup(x => x.GetIdentityDescriptor(ADO_ORG, TEAM_PROJECT_ID, "Project Valid Users").Result).Returns(IDENTITY_DESCRIPTOR);

        var args = new LockRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.LockRepo(ADO_ORG, TEAM_PROJECT_ID, REPO_ID, IDENTITY_DESCRIPTOR));
    }
}
