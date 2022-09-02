using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.BbsToGithub;
using OctoshiftCLI.BbsToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.BbsToGithub.Commands
{
    public class MigrateRepoCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<BbsApi> _mockBbsApi = TestHelpers.CreateMock<BbsApi>();
        private readonly Mock<BbsApiFactory> _mockBbsApiFactory = TestHelpers.CreateMock<BbsApiFactory>();
        private readonly Mock<AzureApi> _mockAzureApi = TestHelpers.CreateMock<AzureApi>();
        private readonly Mock<IAzureApiFactory> _mockAzureApiFactory = new Mock<IAzureApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();

        private readonly MigrateRepoCommand _command;

        private const string ARCHIVE_PATH = "path/to/archive.tar";
        private const string ARCHIVE_URL = "https://archive-url/bbs-archive.tar";
        private const string GITHUB_ORG = "target-org";
        private const string GITHUB_REPO = "target-repo";
        private const string GITHUB_PAT = "github pat";

        private const string BBS_SERVER_URL = "https://our-bbs-server.com";
        private const string BBS_USERNAME = "bbs-username";
        private const string BBS_PASSWORD = "bbs-password";
        private const string BBS_PROJECT = "bbs-project";
        private const string BBS_REPO = "bbs-repo";
        private const long BBS_EXPORT_ID = 123;

        private const string GITHUB_ORG_ID = "github-org-id";
        private const string MIGRATION_SOURCE_ID = "migration-source-id";
        private const string MIGRATION_ID = "migration-id";

        public MigrateRepoCommandTests()
        {
            _command = new MigrateRepoCommand(
                _mockOctoLogger.Object,
                _mockGithubApiFactory.Object,
                _mockBbsApiFactory.Object,
                _mockAzureApiFactory.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockFileSystemProvider.Object
            );
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("migrate-repo");
            _command.Options.Count.Should().Be(13);

            TestHelpers.VerifyCommandOption(_command.Options, "archive-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.GithubPersonalAccessToken()).Returns(GITHUB_PAT);
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Uses_GitHub_Pat_When_Provided_As_Option()
        {
            // Arrange
            var githubPat = "specific github pat";

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = githubPat
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                githubPat,
                ARCHIVE_URL
            ));
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.GithubPersonalAccessToken()).Returns(GITHUB_PAT);
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _command.Invoke(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
        }

        [Fact]
        public async Task Hits_Bitbucket_With_The_Right_Options()
        {
            // Arrange
            _mockBbsApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(_mockBbsApi.Object);

            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("COMPLETED", "The export is complete", 100));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO
            };
            await _command.Invoke(args);

            // Assert
            _mockBbsApi.Verify(m => m.StartExport(
                BBS_PROJECT,
                BBS_REPO
            ));

        }

        [Fact]
        public async Task Throws_An_Error_If_Export_Fails()
        {
            // Arrange
            _mockBbsApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(_mockBbsApi.Object);

            _mockBbsApi.Setup(x => x.StartExport(BBS_PROJECT, BBS_REPO)).ReturnsAsync(BBS_EXPORT_ID);
            _mockBbsApi.Setup(x => x.GetExport(BBS_EXPORT_ID)).ReturnsAsync(("FAILED", "The export failed", 0));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                BbsServerUrl = BBS_SERVER_URL,
                BbsUsername = BBS_USERNAME,
                BbsPassword = BBS_PASSWORD,
                BbsProject = BBS_PROJECT,
                BbsRepo = BBS_REPO,
                Wait = true
            };

            // Assert
            await _command.Invoking(x => x.Invoke(args)).Should().ThrowExactlyAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Uses_Archive_Path_If_Provided()
        {
            // Arrange
            _mockEnvironmentVariableProvider.Setup(m => m.GithubPersonalAccessToken()).Returns(GITHUB_PAT);
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var archiveBytes = Encoding.ASCII.GetBytes("here are some bytes");
            _mockFileSystemProvider.Setup(x => x.ReadAllBytesAsync(ARCHIVE_PATH)).ReturnsAsync(archiveBytes);

            _mockAzureApiFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), archiveBytes)).ReturnsAsync(new System.Uri(ARCHIVE_URL));

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartBbsMigration(MIGRATION_SOURCE_ID, GITHUB_ORG_ID, GITHUB_REPO, GITHUB_PAT, ARCHIVE_URL).Result)
                .Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchivePath = ARCHIVE_PATH,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartBbsMigration(
                MIGRATION_SOURCE_ID,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                GITHUB_PAT,
                ARCHIVE_URL
            ));
        }
    }
}
