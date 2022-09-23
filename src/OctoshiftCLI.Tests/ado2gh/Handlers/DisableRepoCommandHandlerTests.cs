using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Octoshift.Models;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.AdoToGithub.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands;

public class DisableRepoCommandHandlerTests
{
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly DisableRepoCommandHandler _handler;

    private const string ADO_ORG = "FooOrg";
    private const string ADO_TEAM_PROJECT = "BlahTeamProject";
    private const string ADO_REPO = "foo-repo";
    private readonly string REPO_ID = Guid.NewGuid().ToString();

    public DisableRepoCommandHandlerTests()
    {
        _handler = new DisableRepoCommandHandler(_mockOctoLogger.Object, _mockAdoApiFactory.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        var repos = new List<AdoRepository> { new() { Id = REPO_ID, Name = ADO_REPO, IsDisabled = false } };

        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);
        _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

        var args = new DisableRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO
        };
        await _handler.Invoke(args);

        _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID));
    }

    [Fact]
    public async Task Idempotency_Repo_Disabled()
    {
        var repos = new List<AdoRepository> { new() { Id = REPO_ID, Name = ADO_REPO, IsDisabled = true } };

        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, ADO_TEAM_PROJECT).Result).Returns(repos);
        _mockAdoApiFactory.Setup(m => m.Create(null)).Returns(_mockAdoApi.Object);

        var args = new DisableRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO
        };
        await _handler.Invoke(args);

        _mockAdoApi.Verify(x => x.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, REPO_ID), Times.Never);
    }

    [Fact]
    public async Task It_Uses_The_Ado_Pat_When_Provided()
    {
        const string adoPat = "ado-pat";

        var repos = new List<AdoRepository> { new() { Id = REPO_ID, Name = ADO_REPO, Size = 1234, IsDisabled = true } };
        _mockAdoApi.Setup(x => x.GetRepos(It.IsAny<string>(), It.IsAny<string>()).Result).Returns(repos);
        _mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(_mockAdoApi.Object);

        var args = new DisableRepoCommandArgs
        {
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
            AdoRepo = ADO_REPO,
            AdoPat = adoPat
        };
        await _handler.Invoke(args);

        _mockAdoApiFactory.Verify(m => m.Create(adoPat));
    }
}
