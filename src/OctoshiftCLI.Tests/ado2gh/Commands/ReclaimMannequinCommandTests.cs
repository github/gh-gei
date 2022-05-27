using System;
using System.Threading.Tasks;
using Moq;
using Octoshift;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ReclaimMannequinCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();

        private readonly ReclaimMannequinCommand _command;

        private const string GITHUB_ORG = "FooOrg";
        private const string MANNEQUIN_USER = "mona";
        private const string TARGET_USER = "mona_emu";

        public ReclaimMannequinCommandTests()
        {
            _command = new ReclaimMannequinCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object, _mockReclaimService.Object)
            {
                FileExists = (s) => true,
                GetFileContent = (s) => Array.Empty<string>()
            };
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("reclaim-mannequin", _command.Name);
            Assert.Equal(8, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "csv", false);
            TestHelpers.VerifyCommandOption(_command.Options, "mannequin-user", false);
            TestHelpers.VerifyCommandOption(_command.Options, "mannequin-id", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-user", false);
            TestHelpers.VerifyCommandOption(_command.Options, "force", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            var mannequinUserId = Guid.NewGuid().ToString();
            var githubPat = "PAT";

            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));
            _mockGithubApiFactory.Setup(m => m.Create(null, githubPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER, false, githubPat);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task SingleReclaiming_Happy_Path()
        {
            string mannequinUserId = null;

            _mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER, false);

            _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
        }

        [Fact]
        public async Task SingleReclaiming_WithIDSpecified_Happy_Path()
        {
            var mannequinUserId = "monaid";

            _mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(GITHUB_ORG, null, MANNEQUIN_USER, mannequinUserId, TARGET_USER, false);

            _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
        }

        [Fact]
        public async Task CSVReclaiming_Happy_Path()
        {
            var githubOrg = "FooOrg";

            _mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequins(Array.Empty<string>(), githubOrg, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(githubOrg, "file.csv", null, null, null, false);

            _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), githubOrg, false), Times.Once);
        }

        [Fact]
        public async Task CSV_CSV_TakesPrecedence()
        {
            _mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, null, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(GITHUB_ORG, "file.csv", MANNEQUIN_USER, null, TARGET_USER, false); // All parameters passed. CSV has precedence

            _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false), Times.Once);
        }
    }
}
