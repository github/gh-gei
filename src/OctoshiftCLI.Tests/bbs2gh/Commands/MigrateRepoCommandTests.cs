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
        private const string GITHUB_API_URL = "https://api.github.com";

        private const string SOURCE_REPO_URL = "https://not-used";
        private const string METADATA_ARCHIVE_URL = "https://not-used";
        private const string SOURCE_TOKEN = "not-used";

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
            _command.Options.Count.Should().Be(7);

            TestHelpers.VerifyCommandOption(_command.Options, "archive-url", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path_Without_Wait()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateBbsMigrationSource(GITHUB_ORG_ID).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi.Setup(x => x.StartMigration(
                MIGRATION_SOURCE_ID,
                SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                SOURCE_TOKEN,
                GITHUB_PAT,
                ARCHIVE_URL,
                METADATA_ARCHIVE_URL,
                false
            ).Result).Returns(MIGRATION_ID);

            _mockEnvironmentVariableProvider.Setup(m => m.GithubPersonalAccessToken()).Returns(SOURCE_TOKEN);

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Repo...",
                $"GITHUB ORG: {GITHUB_ORG}",
                $"GITHUB REPO: {GITHUB_REPO}",
                "GITHUB PAT: ***",
                $"GITHUB API URL: {GITHUB_API_URL}",
                $"A repository migration (ID: {MIGRATION_ID}) was successfully queued."
            };

            // Act
            var args = new MigrateRepoCommandArgs
            {
                ArchiveUrl = ARCHIVE_URL,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                GithubPat = GITHUB_PAT,
                GithubApiUrl = GITHUB_API_URL
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetOrganizationId(GITHUB_ORG));
            _mockGithubApi.Verify(m => m.CreateBbsMigrationSource(GITHUB_ORG_ID));
            _mockGithubApi.Verify(m => m.StartMigration(
                MIGRATION_SOURCE_ID,
                SOURCE_REPO_URL,
                GITHUB_ORG_ID,
                GITHUB_REPO,
                SOURCE_TOKEN,
                GITHUB_PAT,
                ARCHIVE_URL,
                METADATA_ARCHIVE_URL,
                false
            ));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(6));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
            _mockOctoLogger.VerifyNoOtherCalls();
        }
    }
}