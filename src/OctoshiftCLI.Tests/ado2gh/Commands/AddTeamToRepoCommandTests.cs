using System.CommandLine;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class AddTeamToRepoCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly AddTeamToRepoCommand _command;

        private readonly string GITHUB_ORG = "foo-org";
        private readonly string GITHUB_REPO = "foo-repo";
        private readonly string TEAM = "foo-team";

        public AddTeamToRepoCommandTests()
        {
            _command = new AddTeamToRepoCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("add-team-to-repo", _command.Name);
            Assert.Equal(6, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "team", true);
            TestHelpers.VerifyCommandOption(_command.Options, "role", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var teamSlug = "foo-slug";
            var role = "maintain";

            _mockGithubApi.Setup(x => x.GetTeamSlug(GITHUB_ORG, TEAM)).ReturnsAsync(teamSlug);
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, GITHUB_REPO, TEAM, role);

            _mockGithubApi.Verify(x => x.AddTeamToRepo(GITHUB_ORG, GITHUB_REPO, teamSlug, role));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, GITHUB_REPO, TEAM, "role", githubPat);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Invalid_Role()
        {
            var role = "read";  // read is not a valid role

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var root = new RootCommand();
            root.AddCommand(_command);
            var args = new string[] { "add-team-to-repo", "--github-org", GITHUB_ORG, "--github-repo", GITHUB_REPO, "--team", TEAM, "--role", role };
            await root.InvokeAsync(args);

            _mockGithubApi.Verify(x => x.AddTeamToRepo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
