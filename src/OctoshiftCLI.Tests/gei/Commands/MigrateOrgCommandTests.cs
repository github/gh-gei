using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateOrgCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly MigrateOrgCommandHandler _command;

        private const string TARGET_ENTERPRISE = "foo-target-ent";
        private const string SOURCE_ORG = "foo-source-org";
        private const string TARGET_ORG = "foo-target-org";

        public MigrateOrgCommandTests()
        {
            _command = new MigrateOrgCommandHandler(
                _mockOctoLogger.Object,
                _mockTargetGithubApiFactory.Object,
                _mockEnvironmentVariableProvider.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateOrgCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object, _mockEnvironmentVariableProvider.Object);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-org");
            command.Options.Count.Should().Be(7);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-enterprise", true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            // Arrange
            var githubOrgId = Guid.NewGuid().ToString();
            var githubEntpriseId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubOrgUrl = $"https://github.com/{SOURCE_ORG}";
            var migrationId = Guid.NewGuid().ToString();
            var migrationState = OrganizationMigrationStatus.Succeeded;

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.GetEnterpriseId(TARGET_ENTERPRISE).Result).Returns(githubEntpriseId);
            _mockGithubApi.Setup(x => x.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat, targetGithubPat).Result).Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetOrganizationMigrationState(migrationId).Result).Returns(migrationState);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Org...",
                $"GITHUB SOURCE ORG: {SOURCE_ORG}",
                $"GITHUB TARGET ORG: {TARGET_ORG}",
                $"GITHUB TARGET ENTERPRISE: {TARGET_ENTERPRISE}",
                $"GITHUB SOURCE PAT: ***",
                $"GITHUB TARGET PAT: ***",
                $"Migration completed (ID: {migrationId})! State: {migrationState}",
            };

            // Act
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubSourcePat = sourceGithubPat,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = targetGithubPat,
                Wait = true,
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetEnterpriseId(TARGET_ENTERPRISE));
            _mockGithubApi.Verify(m => m.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat, targetGithubPat));
            _mockGithubApi.Verify(m => m.GetOrganizationMigrationState(migrationId));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(6));
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
            _mockOctoLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Happy_Path_Only_Target_PAT()
        {
            // Arrange
            var githubOrgId = Guid.NewGuid().ToString();
            var githubEntpriseId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubOrgUrl = $"https://github.com/{SOURCE_ORG}";
            var migrationId = Guid.NewGuid().ToString();
            var migrationState = OrganizationMigrationStatus.Succeeded;

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.GetEnterpriseId(TARGET_ENTERPRISE).Result).Returns(githubEntpriseId);
            _mockGithubApi.Setup(x => x.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, targetGithubPat, targetGithubPat).Result).Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetOrganizationMigrationState(migrationId).Result).Returns(migrationState);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Org...",
                $"GITHUB SOURCE ORG: {SOURCE_ORG}",
                $"GITHUB TARGET ORG: {TARGET_ORG}",
                $"GITHUB TARGET ENTERPRISE: {TARGET_ENTERPRISE}",
                $"GITHUB TARGET PAT: ***",
                $"Since github-target-pat is provided, github-source-pat will also use its value.",
                $"Migration completed (ID: {migrationId})! State: {migrationState}"
            };

            // Act
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                GithubTargetPat = targetGithubPat,
                Wait = true,
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetEnterpriseId(TARGET_ENTERPRISE));
            _mockGithubApi.Verify(m => m.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, targetGithubPat, targetGithubPat));
            _mockGithubApi.Verify(m => m.GetOrganizationMigrationState(migrationId));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(6));
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
            _mockOctoLogger.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Happy_Path_PAT_In_Env()
        {
            // Arrange
            var githubOrgId = Guid.NewGuid().ToString();
            var githubEntpriseId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubOrgUrl = $"https://github.com/{SOURCE_ORG}";
            var migrationId = Guid.NewGuid().ToString();
            var migrationState = OrganizationMigrationStatus.Succeeded;

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.GetEnterpriseId(TARGET_ENTERPRISE).Result).Returns(githubEntpriseId);
            _mockGithubApi.Setup(x => x.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat, targetGithubPat).Result).Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetOrganizationMigrationState(migrationId).Result).Returns(migrationState);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Org...",
                $"GITHUB SOURCE ORG: {SOURCE_ORG}",
                $"GITHUB TARGET ORG: {TARGET_ORG}",
                $"GITHUB TARGET ENTERPRISE: {TARGET_ENTERPRISE}",
                $"Migration completed (ID: {migrationId})! State: {migrationState}",
            };

            // Act
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                Wait = true,
            };
            await _command.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetEnterpriseId(TARGET_ENTERPRISE));
            _mockGithubApi.Verify(m => m.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat, targetGithubPat));
            _mockGithubApi.Verify(m => m.GetOrganizationMigrationState(migrationId));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(4));
            _mockOctoLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
            _mockOctoLogger.VerifyNoOtherCalls();
        }
    }
}
