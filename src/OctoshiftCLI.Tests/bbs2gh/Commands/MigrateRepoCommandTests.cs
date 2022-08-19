using System.Collections.Generic;
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
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly MigrateRepoCommand _command;

        private const string ARCHIVE_URL = "https://archive-url/bbs-archive.tar";
        private const string GITHUB_ORG = "target-org";
        private const string GITHUB_REPO = "target-repo";
        private const string GITHUB_PAT = "github pat";

        private const string UNUSED_SOURCE_REPO_URL = "https://not-used";
        private const string UNUSED_METADATA_ARCHIVE_URL = "https://not-used";
        private const string UNUSED_SOURCE_TOKEN = "not-used";

        private const string GITHUB_ORG_ID = "github-org-id";
        private const string MIGRATION_SOURCE_ID = "migration-source-id";
        private const string MIGRATION_ID = "migration-id";

        public MigrateRepoCommandTests()
        {
            _command = new MigrateRepoCommand(
                _mockOctoLogger.Object,
                _mockGithubApiFactory.Object,
                _mockEnvironmentVariableProvider.Object
            );
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("migrate-repo");
            _command.Options.Count.Should().Be(6);

            TestHelpers.VerifyCommandOption(_command.Options, "archive-url", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
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
            _mockGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                UNUSED_SOURCE_TOKEN,
                GITHUB_PAT,
                ARCHIVE_URL,
                UNUSED_METADATA_ARCHIVE_URL,
                false
            ).Result).Returns(MIGRATION_ID);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetOrganizationId(GITHUB_ORG));
            _mockGithubApi.Verify(m => m.CreateBbsMigrationSource(GITHUB_ORG_ID));
            _mockGithubApi.Verify(m => m.StartMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                UNUSED_SOURCE_TOKEN,
                GITHUB_PAT,
                ARCHIVE_URL,
                UNUSED_METADATA_ARCHIVE_URL,
                false
            ));
        }

        [Fact]
        public async Task Uses_GitHub_Pat_When_Provided_As_Option()
        {
            // Arrange
            var githubPat = "specific github pat";

            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                UNUSED_SOURCE_TOKEN,
                githubPat,
                ARCHIVE_URL,
                UNUSED_METADATA_ARCHIVE_URL,
                false
            ).Result).Returns(MIGRATION_ID);

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

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
            _mockGithubApi.Verify(m => m.GetOrganizationId(GITHUB_ORG));
            _mockGithubApi.Verify(m => m.CreateBbsMigrationSource(GITHUB_ORG_ID));
            _mockGithubApi.Verify(m => m.StartMigration(
                MIGRATION_SOURCE_ID,
                UNUSED_SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                UNUSED_SOURCE_TOKEN,
                githubPat,
                ARCHIVE_URL,
                UNUSED_METADATA_ARCHIVE_URL,
                false
            ));
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    UNUSED_SOURCE_REPO_URL,
                    GITHUB_ORG_ID,
                    GITHUB_REPO,
                    UNUSED_SOURCE_TOKEN,
                    GITHUB_PAT,
                    ARCHIVE_URL,
                    UNUSED_METADATA_ARCHIVE_URL,
                    false
                ).Result)
                .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            _mockEnvironmentVariableProvider.Setup(m => m.GithubPersonalAccessToken()).Returns(GITHUB_PAT);

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = $"The Org '{GITHUB_ORG}' already contains a repository with the name '{GITHUB_REPO}'. No operation will be performed";

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
            actualLogOutput.Should().Contain(expectedLogOutput);
        }
    }
}
