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
    public class MigrateRepoCommandTests
    {
        private const string TARGET_API_URL = "https://api.github.com";
        private const string GHES_API_URL = "https://api.ghes.com";
        private const string AZURE_CONNECTION_STRING = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
        private const string SOURCE_ORG = "foo-source-org";
        private const string SOURCE_REPO = "foo-repo-source";
        private const string TARGET_ORG = "foo-target-org";
        private const string TARGET_REPO = "foo-target-repo";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand(null, null, null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(15);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-source-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "source-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "azure-storage-connection-string", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "git-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "metadata-archive-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, TARGET_REPO, sourceGithubPat, targetGithubPat, "", "").Result).Returns(migrationId);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>()
            {
                "Migrating Repo...",
                $"GITHUB SOURCE ORG: {SOURCE_ORG}",
                $"SOURCE REPO: {SOURCE_REPO}",
                $"GITHUB TARGET ORG: {TARGET_ORG}",
                $"TARGET REPO: {TARGET_REPO}",
                $"Target API URL: {TARGET_API_URL}",
                $"A repository migration (ID: {migrationId}) was successfully queued."
            };

            // Act
            var command = new MigrateRepoCommand(mockLogger.Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, wait: false);

            // Assert
            mockGithub.Verify(m => m.GetOrganizationId(TARGET_ORG));
            mockGithub.Verify(m => m.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat));
            mockGithub.Verify(m => m.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, TARGET_REPO, sourceGithubPat, targetGithubPat, "", ""));

            mockLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(7));
            actualLogOutput.Should().Equal(expectedLogOutput);

            mockGithub.VerifyNoOtherCalls();
            mockLogger.VerifyNoOtherCalls();
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    "",
                    "").Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, sourceAdoPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    adoRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceAdoPat,
                    targetGithubPat,
                    "",
                    "").Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(sourceAdoPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(null, SOURCE_ORG, adoTeamProject, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Happy_Path_GithubSource_GHES()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString()).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            var mockAzureApi = new Mock<AzureApi>(null, null);
            mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(AZURE_CONNECTION_STRING)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, GHES_API_URL, AZURE_CONNECTION_STRING, wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    gitArchiveUrl,
                    metadataArchiveUrl).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, "", "", false, gitArchiveUrl, metadataArchiveUrl, wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Github_Only_One_Archive_Url_Throws_Error()
        {
            var gitArchiveUrl = "https://example.com/git_archive.tar.gz";

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, "", "", false, gitArchiveUrl, wait: true))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task No_Source_Provided_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, ""))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ado_Source_Without_Team_Project_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, SOURCE_ORG, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, ""))
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    SOURCE_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    "",
                    "").Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, null, "", wait: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ghes_Without_AzureConnectionString_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, GHES_API_URL, ""))
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
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString()).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            var mockAzureApi = new Mock<AzureApi>(null, null);
            mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);
            environmentVariableProviderMock.Setup(m => m.AzureStorageConnectionString()).Returns(azureConnectionStringEnv);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(azureConnectionStringEnv)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, GHES_API_URL, wait: true);

            mockAzureApiFactory.Verify(x => x.Create(azureConnectionStringEnv));
            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ghes_With_NoSslVerify_Uses_NoSsl_Client()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{SOURCE_ORG}/{SOURCE_REPO}";
            var migrationId = Guid.NewGuid().ToString();
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var gitArchiveUrl = $"https://example.com/{gitArchiveId}";
            var metadataArchiveUrl = $"https://example.com/{metadataArchiveId}";
            var gitArchiveContent = new byte[] { 1, 2, 3, 4, 5 };
            var metadataArchiveContent = new byte[] { 6, 7, 8, 9, 10 };
            var authenticatedGitArchiveUrl = new Uri($"https://example.com/{gitArchiveId}/authenticated");
            var authenticatedMetadataArchiveUrl = new Uri($"https://example.com/{metadataArchiveId}/authenticated");

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(TARGET_ORG).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat).Result).Returns(migrationSourceId);
            mockGithub
                .Setup(x => x.StartMigration(
                    migrationSourceId,
                    githubRepoUrl,
                    githubOrgId,
                    TARGET_REPO,
                    sourceGithubPat,
                    targetGithubPat,
                    authenticatedGitArchiveUrl.ToString(),
                    authenticatedMetadataArchiveUrl.ToString()).Result)
                .Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(SOURCE_ORG, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            var mockAzureApi = new Mock<AzureApi>(null, null);
            mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.CreateClientNoSsl(GHES_API_URL, It.IsAny<string>())).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(TARGET_API_URL, It.IsAny<string>())).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.CreateClientNoSsl(AZURE_CONNECTION_STRING)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, GHES_API_URL, AZURE_CONNECTION_STRING, true, wait: true);

            mockAzureApiFactory.Verify(x => x.CreateClientNoSsl(AZURE_CONNECTION_STRING));
            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ghes_Failed_Archive_Generation_Throws_Error()
        {
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(SOURCE_ORG, SOURCE_REPO).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(SOURCE_ORG, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Failed);

            var mockAzureApi = new Mock<AzureApi>(null, null);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_API_URL, It.IsAny<string>())).Returns(mockGhesGithubApi.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(AZURE_CONNECTION_STRING)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, null, null, mockAzureApiFactory.Object);
            await FluentActions
                .Invoking(async () => await command.Invoke(SOURCE_ORG, null, null, SOURCE_REPO, TARGET_ORG, TARGET_REPO, TARGET_API_URL, GHES_API_URL, AZURE_CONNECTION_STRING))
                .Should().ThrowAsync<OctoshiftCliException>();
        }
    }
}
