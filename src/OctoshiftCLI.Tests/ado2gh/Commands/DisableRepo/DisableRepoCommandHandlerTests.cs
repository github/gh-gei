using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.AdoToGithub.Commands.DisableRepo;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.DisableRepo;

public class DisableRepoCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly DisableRepoCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string ADO_REPO = "foo-repo";
    private readonly string REPO_ID = Guid.NewGuid().ToString();

    public DisableRepoCommandHandlerTests()
    {
        _handler = new DisableRepoCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var repos = new List<AdoRepository> { new() { Id = REPO_ID, Name = ADO_REPO, IsDisabled = false } };

        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);

        var args = new DisableRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID));
    }

    [Fact]
    public async Task Idempotency_Repo_Disabled()
    {
        var repos = new List<AdoRepository> { new() { Id = REPO_ID, Name = ADO_REPO, IsDisabled = true } };

        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);

        var args = new DisableRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO
        };
        await _handler.Handle(args);

        _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID), Times.Never);
    }
}
