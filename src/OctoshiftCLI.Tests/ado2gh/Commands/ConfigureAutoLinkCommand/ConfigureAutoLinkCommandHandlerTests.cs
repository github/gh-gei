using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.ConfigureAutoLink;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.ConfigureAutoLink;

public class ConfigureAutoLinkCommandHandlerTests
{
    private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

    private readonly ConfigureAutoLinkCommandHandler _handler;

    private const string GITHUB_ORG = "foo-org";
    private const string GITHUB_REPO = "foo-repo";
    private const string ADO_ORG = "foo-ado-org";
    private const string ADO_TEAM_PROJECT = "foo-ado-tp";
    private const string KEY_PREFIX = "AB#";
    private readonly string URL_TEMPLATE = $"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_workitems/edit/<num>/";

    public ConfigureAutoLinkCommandHandlerTests()
    {
        _handler = new ConfigureAutoLinkCommandHandler(_mockOctoLogger.Object, _mockGithubApi.Object);
    }

    [Fact]
    public async Task Happy_Path()
    {
        _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync([]);

        var args = new ConfigureAutoLinkCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1), Times.Never);
        _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE));
    }

    [Fact]
    public async Task Idempotency_AutoLink_Exists()
    {
        _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.FromResult(new List<(int Id, string KeyPrefix, string UrlTemplate)>
                  {
                      (1, KEY_PREFIX, URL_TEMPLATE),
                  }));

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var args = new ConfigureAutoLinkCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1), Times.Never);
        _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE), Times.Never);
        actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{KEY_PREFIX}'. No operation will be performed");
    }

    [Fact]
    public async Task Idempotency_KeyPrefix_Exists()
    {
        _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync(
                  [
                      (1, KEY_PREFIX, "SomethingElse"),
                  ]);

        var actualLogOutput = new List<string>();
        _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
        _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

        var args = new ConfigureAutoLinkCommandArgs
        {
            GithubOrg = GITHUB_ORG,
            GithubRepo = GITHUB_REPO,
            AdoOrg = ADO_ORG,
            AdoTeamProject = ADO_TEAM_PROJECT,
        };
        await _handler.Handle(args);

        _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1));
        _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE));
        actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{KEY_PREFIX}', but the url template is incorrect");
        actualLogOutput.Should().Contain($"Deleting existing Autolink reference for key_prefix: '{KEY_PREFIX}' before creating a new Autolink reference");
    }
}
