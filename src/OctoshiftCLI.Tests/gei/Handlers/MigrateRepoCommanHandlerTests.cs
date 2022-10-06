using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.GithubEnterpriseImporter.Handlers;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class MigrateRepoCommandHandlerTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ISourceGithubApiFactory> _mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<AzureApi> _mockAzureApi = TestHelpers.CreateMock<AzureApi>();
        private readonly Mock<IAzureApiFactory> _mockAzureApiFactory = new Mock<IAzureApiFactory>();
        private readonly Mock<AwsApiFactory> _mockAwsApiFactory = TestHelpers.CreateMock<AwsApiFactory>();
        private readonly Mock<AwsApi> _mockAwsApi = TestHelpers.CreateMock<AwsApi>();

        private readonly MigrateRepoCommandHandler _handler;

        private const string TARGET_API_URL = "https://api.github.com";
        private const string GHES_API_URL = "https://myghes/api/v3";
        private const string AZURE_CONNECTION_STRING = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
        private const string SOURCE_ORG = "foo-source-org";
        private const string SOURCE_REPO = "foo-repo-source";
        private const string TARGET_ORG = "foo-target-org";
        private const string TARGET_REPO = "foo-target-repo";
        private const string ADO_PAT = "ado-pat";
        private const string GITHUB_TARGET_PAT = "github-target-pat";
        private const string GITHUB_SOURCE_PAT = "github-source-pat";

        public MigrateRepoCommandHandlerTests()
        {
            _handler = new MigrateRepoCommandHandler(
                _mockOctoLogger.Object,
                _mockSourceGithubApiFactory.Object,
                _mockTargetGithubApiFactory.Object,
                _mockEnvironmentVariableProvider.Object,
                _mockAzureApiFactory.Object,
                _mockAwsApiFactory.Object);
        }

        [Fact]
        public async Task Happy_Path_Without_Wait()
        {
            // Arrange
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, TARGET_REPO, sourceGithubPat, targetGithubPat, null, null, false, false).Result).Returns(migrationId);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Repo...",
                $"GITHUB SOURCE ORG: {SOURCE_ORG}",
                $"SOURCE REPO: {SOURCE_REPO}",
                $"GITHUB TARGET ORG: {TARGET_ORG}",
                $"TARGET REPO: {TARGET_REPO}",
                $"TARGET API URL: {TARGET_API_URL}",
                $"A repository migration (ID: {migrationId}) was successfully queued."
            };

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                Wait = false
            };
            await _handler.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.GetOrganizationId(TARGET_ORG));
            _mockGithubApi.Verify(m => m.CreateGhecMigrationSource(githubOrgId));
            _mockGithubApi.Verify(m => m.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, TARGET_REPO, sourceGithubPat, targetGithubPat, null, null, false, false));

            _mockOctoLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
            actualLogOutput.Should().Equal(expectedLogOutput);

            _mockGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Skip_Migration_If_Target_Repo_Exists()
        {
            // Arrange
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, TARGET_REPO, sourceGithubPat, targetGithubPat, null, null, false, false).Result)
                .Throws(new OctoshiftCliException($"A repository called {TARGET_ORG}/{TARGET_REPO} already exists"));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            _mockOctoLogger.Setup(m => m.LogWarning(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = $"The Org '{TARGET_ORG}' already contains a repository with the name '{TARGET_REPO}'. No operation will be performed";

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                Wait = false
            };
            await _handler.Invoke(args);

            // Assert
            _mockOctoLogger.Verify(m => m.LogWarning(It.IsAny<string>()), Times.Exactly(1));
            actualLogOutput.Should().Contain(expectedLogOutput);
        }

        [Fact]
        public async Task Happy_Path_GithubSource()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    null,
                    null,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Happy_Path_AdoSource()
        {
            var adoTeamProject = "foo-team-project";

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceAdoPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var adoRepoUrl = $"https://dev.azure.com/{SOURCE_ORG}/{adoTeamProject}/_git/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(githubOrgId, null).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    adoRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceAdoPat,
                    targetGithubPat,
                    null,
                    null,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockEnvironmentVariableProvider.Setup(m => m.AdoPersonalAccessToken()).Returns(sourceAdoPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Happy_Path_AdoServerSource()
        {
            var adoTeamProject = "foo-team-project";
            var adoServerUrl = "https://ado.contoso.com";

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceAdoPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var adoRepoUrl = $"{adoServerUrl}/{SOURCE_ORG}/{adoTeamProject}/_git/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateAdoMigrationSource(githubOrgId, adoServerUrl).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    adoRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceAdoPat,
                    targetGithubPat,
                    null,
                    null,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockEnvironmentVariableProvider.Setup(m => m.AdoPersonalAccessToken()).Returns(sourceAdoPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                AdoServerUrl = adoServerUrl,
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = adoTeamProject,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Happy_Path_GithubSource_Ghes()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://myghes/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString(),
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            _mockGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(metadataArchiveId);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            _mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            _mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockAzureApiFactory.Setup(m => m.Create(AZURE_CONNECTION_STRING)).Returns(_mockAzureApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
            _mockOctoLogger.Verify(x => x.LogInformation($"GHES API URL: {GHES_API_URL}"), Times.Once);
            _mockOctoLogger.Verify(x => x.LogInformation("AZURE STORAGE CONNECTION STRING: ***"), Times.Once);
        }

        [Fact]
        public async Task AdoServer_Source_Without_SourceOrg_Provided_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    AdoServerUrl = "https://ado.contoso.com",
                    AdoTeamProject = "FooProj",
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Github_With_Archive_Urls()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            var gitArchiveUrl = "https://example.com/git_archive.tar.gz";
            var metadataArchiveUrl = "https://example.com/metadata_archive.tar.gz";

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    gitArchiveUrl,
                    metadataArchiveUrl,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GitArchiveUrl = gitArchiveUrl,
                MetadataArchiveUrl = metadataArchiveUrl,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Github_Only_One_Archive_Url_Throws_Error()
        {
            var gitArchiveUrl = "https://example.com/git_archive.tar.gz";

            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    GithubSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = TARGET_API_URL,
                    GitArchiveUrl = gitArchiveUrl,
                    Wait = true
                }))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task No_Source_Provided_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = ""
                }))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ado_Source_Without_Team_Project_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    AdoSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = ""
                }))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Defaults_TARGET_REPO_To_SourceRepo()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    SOURCE_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    null,
                    null,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, SOURCE_REPO, null));

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Ghes_Without_AzureConnectionString_Throws_Error()
        {
            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    GhesApiUrl = GHES_API_URL
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ghes_AzureConnectionString_Uses_Env_When_Option_Empty()
        {
            var azureConnectionStringEnv = Guid.NewGuid().ToString();

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://myghes/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString(),
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            _mockGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(metadataArchiveId);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            _mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            _mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.AzureStorageConnectionString()).Returns(azureConnectionStringEnv);

            _mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(azureConnectionStringEnv)).Returns(_mockAzureApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockAzureApiFactory.Verify(x => x.Create(azureConnectionStringEnv));
            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
        }

        [Fact]
        public async Task Ghes_With_NoSslVerify_Uses_NoSsl_Client()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://myghes/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString(),
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            _mockGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(metadataArchiveId);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            _mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            _mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockSourceGithubApiFactory.Setup(m => m.CreateClientNoSsl(GHES_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.CreateClientNoSsl(AZURE_CONNECTION_STRING)).Returns(_mockAzureApi.Object);

            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                NoSslVerify = true,
                Wait = true
            };
            await _handler.Invoke(args);

            _mockAzureApiFactory.Verify(x => x.CreateClientNoSsl(AZURE_CONNECTION_STRING));
            _mockGithubApi.Verify(x => x.GetMigration(migrationId));
            _mockOctoLogger.Verify(x => x.LogInformation("SSL verification disabled"));
        }

        [Fact]
        public async Task Ghes_Failed_Archive_Generation_Throws_Error()
        {
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            _mockGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            _mockGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(metadataArchiveId);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Failed);

            _mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(AZURE_CONNECTION_STRING)).Returns(_mockAzureApi.Object);

            await FluentActions
                .Invoking(async () => await _handler.Invoke(new MigrateRepoCommandArgs
                {
                    GithubSourceOrg = SOURCE_ORG,
                    SourceRepo = SOURCE_REPO,
                    GithubTargetOrg = TARGET_ORG,
                    TargetRepo = TARGET_REPO,
                    TargetApiUrl = TARGET_API_URL,
                    GhesApiUrl = GHES_API_URL,
                    AzureStorageConnectionString = AZURE_CONNECTION_STRING
                }
                ))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task It_Uses_Ado_Pat_When_Provided()
        {
            // Arrange
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                AdoSourceOrg = SOURCE_ORG,
                AdoTeamProject = "adoTeamProject",
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                AdoPat = ADO_PAT
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().Contain("ADO PAT: ***");
            actualLogOutput.Should().NotContain("GITHUB SOURCE PAT: ***");
            actualLogOutput.Should().NotContain("GITHUB TARGET PAT: ***");
            actualLogOutput.Should().NotContain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.AdoPersonalAccessToken(), Times.Never);
            _mockGithubApi.Verify(m => m.CreateAdoMigrationSource(It.IsAny<string>(), null));
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                ADO_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Uses_Github_Source_And_Target_Pats_When_Provided()
        {
            // Arrange
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), GITHUB_TARGET_PAT)).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GithubTargetPat = GITHUB_TARGET_PAT,
                GithubSourcePat = GITHUB_SOURCE_PAT
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().NotContain("ADO PAT: ***");
            actualLogOutput.Should().Contain("GITHUB SOURCE PAT: ***");
            actualLogOutput.Should().Contain("GITHUB TARGET PAT: ***");
            actualLogOutput.Should().NotContain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(), Times.Never);
            _mockGithubApi.Verify(m => m.CreateGhecMigrationSource(It.IsAny<string>()));
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                GITHUB_SOURCE_PAT,
                GITHUB_TARGET_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Uses_Github_Source_Pat_When_Provided()
        {
            // Arrange
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockAzureApi.Setup(m => m.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://example.com/resource"));

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), GITHUB_SOURCE_PAT)).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), null)).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(AZURE_CONNECTION_STRING)).Returns(_mockAzureApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                GithubSourcePat = GITHUB_SOURCE_PAT
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().NotContain("ADO PAT: ***");
            actualLogOutput.Should().Contain("GITHUB SOURCE PAT: ***");
            actualLogOutput.Should().NotContain("GITHUB TARGET PAT: ***");
            actualLogOutput.Should().NotContain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken());
            _mockGithubApi.Verify(m => m.CreateGhecMigrationSource(It.IsAny<string>()));
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                GITHUB_SOURCE_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Falls_Back_To_Github_Target_Pat_If_Github_Source_Pat_Is_Not_Provided()
        {
            // Arrange
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), GITHUB_TARGET_PAT)).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                GithubTargetPat = GITHUB_TARGET_PAT
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().NotContain("ADO PAT: ***");
            actualLogOutput.Should().NotContain("GITHUB SOURCE PAT: ***");
            actualLogOutput.Should().Contain("GITHUB TARGET PAT: ***");
            actualLogOutput.Should().Contain("Since github-target-pat is provided, github-source-pat will also use its value.");

            _mockEnvironmentVariableProvider.Verify(m => m.SourceGithubPersonalAccessToken(), Times.Never);
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(), Times.Never);
            _mockGithubApi.Verify(m => m.CreateGhecMigrationSource(It.IsAny<string>()));
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                GITHUB_TARGET_PAT,
                GITHUB_TARGET_PAT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Skips_Releases_When_Option_Is_True()
        {
            // Arrange
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                SkipReleases = true
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().Contain("SKIP RELEASES: true");

            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                true,
                It.IsAny<bool>()));
        }

        [Fact]
        public async Task It_Locks_Source_Repo_When_Option_Is_True()
        {
            // Arrange
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                SkipReleases = false,
                LockSourceRepo = true
            };
            await _handler.Invoke(args);

            // Assert
            actualLogOutput.Should().Contain("LOCK SOURCE REPO: true");

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
                true));
        }

        [Fact]
        public async Task Does_Not_Pass_Lock_Repos_To_StartMigration_For_GHES()
        {
            // Arrange
            _mockGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);

            _mockAzureApi.Setup(x => x.DownloadArchive(It.IsAny<string>()).Result).Returns(Array.Empty<byte>());
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://example.com/resource"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true,
                LockSourceRepo = true
            };
            await _handler.Invoke(args);

            // Assert
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
                false));
        }

        [Fact]
        public async Task It_Skips_Releases_When_Option_Is_True_For_Ghes_Migration_Path()
        {
            // Arrange
            _mockGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);

            _mockAzureApi.Setup(x => x.DownloadArchive(It.IsAny<string>()).Result).Returns(Array.Empty<byte>());
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://example.com/resource"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true
            };
            await _handler.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, true, false));
        }

        [Fact]
        public async Task It_Locks_Repos_When_Option_Is_True_For_Ghes_Migration_Path()
        {
            // Arrange
            _mockGithubApi.Setup(m => m.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);

            _mockAzureApi.Setup(x => x.DownloadArchive(It.IsAny<string>()).Result).Returns(Array.Empty<byte>());
            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://example.com/resource"));

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING,
                SkipReleases = true,
                LockSourceRepo = true
            };
            await _handler.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, true, true));
        }

        [Fact]
        public async Task It_Extracts_Base_Ghes_Url_From_Ghes_Api_Url_Using_Alternate_Template()
        {
            // Arrange
            const string ghesApiUrl = "https://api.myghes.com";
            var expectedGithubRepoUrl = $"https://myghes.com/{SOURCE_ORG}/{SOURCE_REPO}";

            _mockGithubApi
                .Setup(x => x.StartMigration(
                    It.IsAny<string>(),
                    expectedGithubRepoUrl,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    false).Result)
                .Returns("migrationId");

            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://blob-url"));

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);

            // act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING
            };
            await _handler.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                expectedGithubRepoUrl,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                false));
        }

        [Fact]
        public async Task It_Falls_Back_To_The_Ghes_Api_Url_If_Could_Not_Extract_Base_Ghes_Url()
        {
            // Arrange
            const string ghesApiUrl = "https://non-conforming-ghes-api-url";
            var expectedGithubRepoUrl = $"{ghesApiUrl}/{SOURCE_ORG}/{SOURCE_REPO}";

            _mockGithubApi
                .Setup(x => x.StartMigration(
                    It.IsAny<string>(),
                    expectedGithubRepoUrl,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    false,
                    false).Result)
                .Returns("migrationId");

            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(It.IsAny<string>(), It.IsAny<int>()).Result).Returns(ArchiveMigrationStatus.Exported);

            _mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), It.IsAny<byte[]>()).Result).Returns(new Uri("https://blob-url"));

            _mockSourceGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);
            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);

            // act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = ghesApiUrl,
                AzureStorageConnectionString = AZURE_CONNECTION_STRING
            };
            await _handler.Invoke(args);

            // Assert
            _mockGithubApi.Verify(m => m.StartMigration(
                It.IsAny<string>(),
                expectedGithubRepoUrl,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                false,
                false));
        }

        [Fact]
        public async Task It_Uses_Aws_If_Arguments_Are_Included()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://myghes/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };

            var awsAccessKey = "awsAccessKey";
            var awsSecretKey = "awsSecretKey";
            var awsBucketName = "awsBucketName";
            var archiveUrl = $"https://s3.amazonaws.com/{awsBucketName}/archive.tar";

            _mockGithubApi.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            _mockGithubApi.Setup(x => x.CreateGhecMigrationSource(githubOrgId).Result).Returns(migrationSourceId);
            _mockGithubApi
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    archiveUrl,
                    archiveUrl,
                    false,
                    false).Result)
                .Returns(migrationId);
            _mockGithubApi.Setup(x => x.GetMigration(migrationId).Result).Returns((State: RepositoryMigrationStatus.Succeeded, TARGET_REPO, null));

            _mockGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            _mockGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO, false, false).Result).Returns(metadataArchiveId);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            _mockGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            _mockAzureApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(_mockAzureApi.Object);
            _mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            _mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);

            _mockEnvironmentVariableProvider.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            _mockEnvironmentVariableProvider.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            _mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(_mockGithubApi.Object);

            _mockAwsApiFactory.Setup(m => m.Create(awsAccessKey, awsSecretKey)).Returns(_mockAwsApi.Object);
            _mockAwsApi.Setup(m => m.UploadBytesToBucket(awsBucketName, It.IsAny<byte[]>(), It.IsAny<string>())).ReturnsAsync(archiveUrl);

            // Act
            var args = new MigrateRepoCommandArgs
            {
                GithubSourceOrg = SOURCE_ORG,
                SourceRepo = SOURCE_REPO,
                GithubTargetOrg = TARGET_ORG,
                TargetRepo = TARGET_REPO,
                TargetApiUrl = TARGET_API_URL,
                GhesApiUrl = GHES_API_URL,
                AwsBucketName = awsBucketName,
                AwsAccessKey = awsAccessKey,
                AwsSecretKey = awsSecretKey,
                Wait = true
            };
            await _handler.Invoke(args);

            // Assert
            _mockAwsApi.Verify(m => m.UploadBytesToBucket(awsBucketName, It.IsAny<byte[]>(), It.IsAny<string>()));
        }
    }
}
