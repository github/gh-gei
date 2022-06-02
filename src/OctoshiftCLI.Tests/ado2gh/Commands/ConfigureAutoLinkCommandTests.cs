using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ConfigureAutoLinkCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ConfigureAutoLinkCommand _command;

        private const string GITHUB_ORG = "foo-org";
        private const string GITHUB_REPO = "foo-repo";
        private const string ADO_ORG = "foo-ado-org";
        private const string ADO_TEAM_PROJECT = "foo-ado-tp";
        private const string KEY_PREFIX = "AB#";
        private readonly string URL_TEMPLATE = $"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_workitems/edit/<num>/".Replace(" ", "%20");

        public ConfigureAutoLinkCommandTests()
        {
            _command = new ConfigureAutoLinkCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("configure-autolink", _command.Name);
            Assert.Equal(6, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>());
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, GITHUB_REPO, ADO_ORG, ADO_TEAM_PROJECT);

            _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1), Times.Never);
            _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>());
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke("githubOrg", "githubRepo", "adoOrg", "adoTeamProject", githubPat);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Idempotency_AutoLink_Exists()
        {
            _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.FromResult(new List<(int Id, string KeyPrefix, string UrlTemplate)>
                      {
                          (1, KEY_PREFIX, URL_TEMPLATE),
                      }));
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            await _command.Invoke(GITHUB_ORG, GITHUB_REPO, ADO_ORG, ADO_TEAM_PROJECT);

            _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1), Times.Never);
            _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE), Times.Never);
            actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{KEY_PREFIX}'. No operation will be performed");
        }

        [Fact]
        public async Task Idempotency_KeyPrefix_Exists()
        {
            _mockGithubApi.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>
                      {
                          (1, KEY_PREFIX, "SomethingElse"),
                      });
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            await _command.Invoke(GITHUB_ORG, GITHUB_REPO, ADO_ORG, ADO_TEAM_PROJECT);

            _mockGithubApi.Verify(x => x.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, 1));
            _mockGithubApi.Verify(x => x.AddAutoLink(GITHUB_ORG, GITHUB_REPO, KEY_PREFIX, URL_TEMPLATE));
            actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{KEY_PREFIX}', but the url template is incorrect");
            actualLogOutput.Should().Contain($"Deleting existing Autolink reference for key_prefix: '{KEY_PREFIX}' before creating a new Autolink reference");
        }
    }
}
