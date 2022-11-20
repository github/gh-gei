using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateOrgCommandHandlerTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly MigrateOrgCommandHandler _handler;

        private const string TARGET_ENTERPRISE = "foo-target-ent";
        private const string SOURCE_ORG = "foo-source-org";
        private const string TARGET_ORG = "foo-target-org";

        public MigrateOrgCommandHandlerTests()
        {
            _handler = new MigrateOrgCommandHandler(
                _mockOctoLogger.Object,
                _mockGithubApi.Object,
                _mockEnvironmentVariableProvider.Object);
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
            _mockGithubApi.Setup(x => x.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat).Result).Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetOrganizationMigration(migrationId).Result)
                .Returns((State: migrationState, SourceOrgUrl: githubOrgUrl, TargetOrgName: TARGET_ORG, FailureReason: null, RemainingRepositoriesCount: 0, TotalRepositoriesCount: 9000));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(targetGithubPat);

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
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetEnterpriseId(TARGET_ENTERPRISE));
            _mockGithubApi.Verify(m => m.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat));
            _mockGithubApi.Verify(m => m.GetOrganizationMigration(migrationId));

            _mockGithubApi.VerifyNoOtherCalls();
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
            _mockGithubApi.Setup(x => x.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat).Result).Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetOrganizationMigration(migrationId).Result).Returns((State: migrationState, SourceOrgUrl: githubOrgUrl, TargetOrgName: TARGET_ORG, FailureReason: null, RemainingRepositoriesCount: 0, TotalRepositoriesCount: 9000));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken(It.IsAny<bool>())).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>())).Returns(targetGithubPat);

            // Act
            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                GithubTargetOrg = TARGET_ORG,
                GithubTargetEnterprise = TARGET_ENTERPRISE,
                Wait = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetEnterpriseId(TARGET_ENTERPRISE));
            _mockGithubApi.Verify(m => m.StartOrganizationMigration(githubOrgUrl, TARGET_ORG, githubEntpriseId, sourceGithubPat));
            _mockGithubApi.Verify(m => m.GetOrganizationMigration(migrationId));

            _mockGithubApi.VerifyNoOtherCalls();
        }
    }
}
