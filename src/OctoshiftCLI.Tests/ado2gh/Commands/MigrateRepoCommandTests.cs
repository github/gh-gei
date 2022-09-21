using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class MigrateRepoCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly MigrateRepoCommandHandler _command;

        private const string ADO_ORG = "FooOrg";
        private const string ADO_TEAM_PROJECT = "BlahTeamProject";
        private const string ADO_REPO = "foo-repo";
        private const string GITHUB_ORG = "foo-gh-org";
        private const string GITHUB_REPO = "gh-repo";
        private readonly string ADO_REPO_URL = $"https://dev.azure.com/{ADO_ORG}/{ADO_TEAM_PROJECT}/_git/{ADO_REPO}";
        private readonly string ADO_TOKEN = Guid.NewGuid().ToString();
        private readonly string GITHUB_ORG_ID = Guid.NewGuid().ToString();
        private readonly string MIGRATION_SOURCE_ID = Guid.NewGuid().ToString();
        private readonly string MIGRATION_ID = Guid.NewGuid().ToString();
        private readonly string GITHUB_TOKEN = Guid.NewGuid().ToString();

        public MigrateRepoCommandTests()
        {
            _command = new MigrateRepoCommandHandler(_mockOctoLogger.Object, _mockGithubApiFactory.Object, _mockEnvironmentVariableProvider.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object, _mockEnvironmentVariableProvider.Object);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(10);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo-visibility", false);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    ADO_REPO_URL,
                    GITHUB_ORG_ID,
                    GITHUB_REPO,
                    ADO_TOKEN,
                    GITHUB_TOKEN,
                    null,
                    null,
                    false,
                    null,
                    false).Result)
                .Returns(MIGRATION_ID);
            _mockGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);
            _mockEnvironmentVariableProvider
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(ADO_TOKEN);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                "Migrating Repo...",
                $"ADO ORG: {ADO_ORG}",
                $"ADO TEAM PROJECT: {ADO_TEAM_PROJECT}",
                $"ADO REPO: {ADO_REPO}",
                $"GITHUB ORG: {GITHUB_ORG}",
                $"GITHUB REPO: {GITHUB_REPO}",
                $"A repository migration (ID: {MIGRATION_ID}) was successfully queued."
            };

            // Act
            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                Wait = false,
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetOrganizationId(GITHUB_ORG));
            _mockGithubApi.Verify(m => m.CreateAdoMigrationSource(GITHUB_ORG_ID, null));
            _mockGithubApi.Verify(m => m.StartMigration(MIGRATION_SOURCE_ID, ADO_REPO_URL, GITHUB_ORG_ID, GITHUB_REPO, ADO_TOKEN, GITHUB_TOKEN, null, null, false, null, false));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
            _mockOctoLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task It_Sets_Target_Repo_Visibility_When_Specified()
        {
            // Arrange
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var targetRepoVisibility = "public";
            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                TargetRepoVisibility = targetRepoVisibility
            };
            await _command.Invoke(args);

            // Assert
            actualLogOutput.Should().Contain("TARGET REPO VISIBILITY: public");

            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                targetRepoVisibility,
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    MIGRATION_SOURCE_ID,
                    ADO_REPO_URL,
                    GITHUB_ORG_ID,
                    GITHUB_REPO,
                    ADO_TOKEN,
                    GITHUB_TOKEN,
                    null,
                    null,
                    false,
                    null,
                    false).Result)
                .Throws(new OctoshiftCliException($"A repository called {GITHUB_ORG}/{GITHUB_REPO} already exists"));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);
            _mockEnvironmentVariableProvider
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(ADO_TOKEN);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = $"The Org '{GITHUB_ORG}' already contains a repository with the name '{GITHUB_REPO}'. No operation will be performed";

            // Act
            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                Wait = false,
            };
            await _command.Invoke(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Contain(expectedLogOutput);
        }

        [Fact]
        public async Task Happy_Path_With_Wait()
        {
            _mockGithubApi.Setup(x => x.GetOrganizationId(GITHUB_ORG).Result).Returns(GITHUB_ORG_ID);
            _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(GITHUB_ORG_ID, null).Result).Returns(MIGRATION_SOURCE_ID);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                        MIGRATION_SOURCE_ID,
                        ADO_REPO_URL,
                        GITHUB_ORG_ID,
                        GITHUB_REPO,
                        ADO_TOKEN,
                        GITHUB_TOKEN,
                        null,
                        null,
                        false,
                        null,
                        false).Result)
                .Returns(MIGRATION_ID);
            _mockGithubApi.Setup(x => x.GetMigration(MIGRATION_ID).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);
            _mockEnvironmentVariableProvider
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(ADO_TOKEN);

            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                Wait = true,
            };
            await _command.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(MIGRATION_ID));
        }

        [Fact]
        public async Task It_Uses_Ado_And_Github_Pats_When_Provided()
        {
            _mockGithubApi.Setup(x => x.GetMigration(It.IsAny<string>()).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), GITHUB_TOKEN)).Returns(_mockGithubApi.Object);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);
            _mockEnvironmentVariableProvider
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(ADO_TOKEN);

            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                Wait = true,
                AdoPat = ADO_TOKEN,
                GithubPat = GITHUB_TOKEN,
            };
            await _command.Invoke(args);

            _mockGithubApiFactory.Verify(m => m.Create(null, GITHUB_TOKEN));
            _mockEnvironmentVariableProvider.Verify(m => m.AdoPersonalAccessToken(), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.GithubPersonalAccessToken(), Times.Never);
        }

        [Fact]
        public async Task It_Falls_Back_To_Ado_And_Github_Pats_From_Environment_When_Not_Provided()
        {
            _mockGithubApi.Setup(x => x.GetRepos(GITHUB_ORG).Result).Returns(new List<string>());
            _mockGithubApi.Setup(x => x.GetMigration(It.IsAny<string>()).Result).Returns((State: RepositoryMigrationStatus.Succeeded, GITHUB_REPO, null));
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), GITHUB_TOKEN)).Returns(_mockGithubApi.Object);

            _mockEnvironmentVariableProvider
                .Setup(m => m.GithubPersonalAccessToken())
                .Returns(GITHUB_TOKEN);
            _mockEnvironmentVariableProvider
                .Setup(m => m.AdoPersonalAccessToken())
                .Returns(ADO_TOKEN);

            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = ADO_ORG,
                AdoTeamProject = ADO_TEAM_PROJECT,
                AdoRepo = ADO_REPO,
                GithubOrg = GITHUB_ORG,
                GithubRepo = GITHUB_REPO,
                Wait = true,
            };
            await _command.Invoke(args);

            _mockGithubApiFactory.Verify(m => m.Create(null, GITHUB_TOKEN));
            _mockEnvironmentVariableProvider.Verify(m => m.AdoPersonalAccessToken());
            _mockEnvironmentVariableProvider.Verify(m => m.GithubPersonalAccessToken());
        }
    }
}
