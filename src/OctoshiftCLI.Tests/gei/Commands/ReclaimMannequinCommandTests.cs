using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class ReclaimMannequinCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<ReclaimService> _mockReclaimService = TestHelpers.CreateMock<ReclaimService>();

        private readonly ReclaimMannequinCommand _command;

        private const string GITHUB_ORG = "FooOrg";
        private const string MANNEQUIN_USER = "mona";
        private const string TARGET_USER = "mona_emu";

        public ReclaimMannequinCommandTests()
        {
            _command = new ReclaimMannequinCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object, _mockReclaimService.Object)
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
            Assert.Equal(9, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "csv", false);
            TestHelpers.VerifyCommandOption(_command.Options, "mannequin-user", false);
            TestHelpers.VerifyCommandOption(_command.Options, "mannequin-id", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-user", false);
            TestHelpers.VerifyCommandOption(_command.Options, "force", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task No_Parameters_Provided_Throws_OctoshiftCliException()
        {
            await FluentActions
                .Invoking(async () => await _command.Invoke(GITHUB_ORG, null, null, null, null, false))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            string mannequinUserId = null;
            var githubTargetPat = "PAT";

            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));
            _mockTargetGithubApiFactory.Setup(m => m.Create(null, githubTargetPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, MANNEQUIN_USER, null, TARGET_USER, mannequinUserId, false, githubTargetPat);

            _mockTargetGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
        }

        [Fact]
        public async Task It_Uses_Target_Api_Url_When_Provided()
        {
            // Arrange
            const string targetApiUrl = "https://api.contoso.com";

            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, null, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));
            _mockTargetGithubApiFactory.Setup(m => m.Create(targetApiUrl, null)).Returns(_mockGithubApi.Object);

            // Act
            await _command.Invoke(GITHUB_ORG, MANNEQUIN_USER, null, TARGET_USER, null, targetApiUrl: targetApiUrl);

            // Assert
            _mockTargetGithubApiFactory.Verify(m => m.Create(targetApiUrl, null));
        }

        [Fact]
        public async Task CSV_CSVFileDoesNotExist_OctoshiftCliException()
        {
            _command.FileExists = _ => false;

            await FluentActions
                .Invoking(async () => await _command.Invoke("dummy", null, null, null, "I_DO_NOT_EXIST_CSV_PATH"))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task SingleReclaiming_Happy_Path()
        {
            string mannequinUserId = null;

            _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(GITHUB_ORG, MANNEQUIN_USER, mannequinUserId, TARGET_USER, null, false);

            _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
        }

        [Fact]
        public async Task SingleReclaiming_WithIdSpecifiedHappy_Path()
        {
            var mannequinUserId = "monaid";

            _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);
            _mockReclaimService.Setup(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false)).Returns(Task.FromResult(default(object)));

            await _command.Invoke(GITHUB_ORG, MANNEQUIN_USER, mannequinUserId, TARGET_USER, null, false);

            _mockReclaimService.Verify(x => x.ReclaimMannequin(MANNEQUIN_USER, mannequinUserId, TARGET_USER, GITHUB_ORG, false), Times.Once);
        }

        [Fact]
        public async Task CSVReclaiming_Happy_Path()
        {
            _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, null, null, null, "file.csv", false);

            _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false), Times.Once);
        }

        [Fact]
        public async Task CSV_CSV_TakesPrecedence()
        {
            _mockTargetGithubApiFactory.Setup(m => m.Create(null, null)).Returns(_mockGithubApi.Object);

            await _command.Invoke(GITHUB_ORG, MANNEQUIN_USER, null, TARGET_USER, "file.csv", false); // All parameters passed. CSV has precedence

            _mockReclaimService.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), GITHUB_ORG, false), Times.Once);
        }
    }
}
