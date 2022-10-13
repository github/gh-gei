using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.AdoToGithub.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands;

public class RewirePipelineCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly RewirePipelineCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string ADO_PIPELINE = "foo-pipeline";
    private const string GITHUB_ORG = "foo-gh-org";
    private const string GITHUB_REPO = "gh-repo";
    private readonly string SERVICE_CONNECTION_ID = Guid.NewGuid().ToString();

    public RewirePipelineCommandHandlerTests()
    {
        _handler = new RewirePipelineCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var pipelineId = 1234;
        var defaultBranch = "default-branch";
        var clean = "true";
        var checkoutSubmodules = "null";

        _mockAdoApi.Setup(x => x.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, ADO_PIPELINE).Result).Returns(pipelineId);
        _mockAdoApi.Setup(x => x.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules));

        var args = new RewirePipelineCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoPipeline = ADO_PIPELINE,
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            ServiceConnectionId = SERVICE_CONNECTION_ID,
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.ChangePipelineRepo(ADO_ORG, ADO_TEAM_PROJECT, pipelineId, defaultBranch, clean, checkoutSubmodules, GITHUB_ORG, GITHUB_REPO, SERVICE_CONNECTION_ID));
    }
}
