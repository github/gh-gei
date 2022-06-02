using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class CreateTeamCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly CreateTeamCommand _command;

        private const string GITHUB_ORG = "FooOrg";
        private const string TEAM_NAME = "foo-team";
        private const string IDP_GROUP = "foo-group";
        private readonly List<string> TEAM_MEMBERS = new() { "dylan", "dave" };
        private const int IDP_GROUP_ID = 42;
        private const string TEAM_SLUG = "foo-slug";

        public CreateTeamCommandTests()
        {
            _command = new CreateTeamCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("create-team", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "team-name", true);
            TestHelpers.VerifyCommandOption(_command.Options, "idp-group", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            _mockGithubApi.Setup(x => x.GetTeamMembers(GITHUB_ORG, TEAM_SLUG).Result).Returns(TEAM_MEMBERS);
            _mockGithubApi.Setup(x => x.GetIdpGroupId(GITHUB_ORG, IDP_GROUP).Result).Returns(IDP_GROUP_ID);
            _mockGithubApi.Setup(x => x.GetTeamSlug(GITHUB_ORG, TEAM_NAME).Result).Returns(TEAM_SLUG);
            _mockGithubApi.Setup(x => x.GetTeams(GITHUB_ORG).Result).Returns(new List<string>());

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, TEAM_NAME, IDP_GROUP);

            _mockGithubApi.Verify(x => x.CreateTeam(GITHUB_ORG, TEAM_NAME));
            _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[0]));
            _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[1]));
            _mockGithubApi.Verify(x => x.AddEmuGroupToTeam(GITHUB_ORG, TEAM_SLUG, IDP_GROUP_ID));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke("GITHUB_ORG", "TEAM_NAME", "IDP_GROUP", githubPat);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Idempotency_Team_Exists()
        {
            _mockGithubApi.Setup(x => x.GetTeamMembers(GITHUB_ORG, TEAM_SLUG).Result).Returns(TEAM_MEMBERS);
            _mockGithubApi.Setup(x => x.GetIdpGroupId(GITHUB_ORG, IDP_GROUP).Result).Returns(IDP_GROUP_ID);
            _mockGithubApi.Setup(x => x.GetTeamSlug(GITHUB_ORG, TEAM_NAME).Result).Returns(TEAM_SLUG);
            _mockGithubApi.Setup(x => x.GetTeams(GITHUB_ORG).Result).Returns(new List<string> { TEAM_NAME });

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            await _command.Invoke(GITHUB_ORG, TEAM_NAME, IDP_GROUP);

            _mockGithubApi.Verify(x => x.CreateTeam(GITHUB_ORG, TEAM_NAME), Times.Never);
            _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[0]));
            _mockGithubApi.Verify(x => x.RemoveTeamMember(GITHUB_ORG, TEAM_SLUG, TEAM_MEMBERS[1]));
            _mockGithubApi.Verify(x => x.AddEmuGroupToTeam(GITHUB_ORG, TEAM_SLUG, IDP_GROUP_ID));
            actualLogOutput.Contains($"Team '{TEAM_NAME}' already exists. New team will not be created");
        }
    }
}
