using System;
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
        private const string Target_Api_Url = "https://api.github.com";
        private const string GHES_Api_Url = "https://api.ghes.com";
        private const string Azure_Connection_String = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
        private const string Source_Org = "foo-source-org";
        private const string Source_Repo = "foo-repo-source";
        private const string Target_Org = "foo-target-org";
        private const string Target_Repo = "foo-target-repo";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateRepoCommand(null, null, null, null, null);
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-repo");
            command.Options.Count.Should().Be(14);

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
            TestHelpers.VerifyCommandOption(command.Options, "git-archive-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "metadata-archive-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "ssh", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path_GithubSource()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url);

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
            var adoRepoUrl = $"https://dev.azure.com/{Source_Org}/{adoTeamProject}/_git/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, sourceAdoPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(sourceAdoPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(null, Source_Org, adoTeamProject, Source_Repo, Target_Org, Target_Repo, Target_Api_Url);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Happy_Path_GithubSource_GHES()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
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
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, authenticatedGitArchiveUrl.ToString(), authenticatedMetadataArchiveUrl.ToString()).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(Source_Org, Source_Repo).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(Source_Org, Source_Repo).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            var mockAzureApi = new Mock<AzureApi>(null, null);
            mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_Api_Url)).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(Azure_Connection_String)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, GHES_Api_Url, Azure_Connection_String);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Github_With_Ssh()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, true).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, ssh: true);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ado_With_Ssh()
        {
            var adoTeamProject = "foo-team-project";

            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceAdoPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var adoRepoUrl = $"https://dev.azure.com/{Source_Org}/{adoTeamProject}/_git/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateAdoMigrationSource(githubOrgId, sourceAdoPat, targetGithubPat, true).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.AdoPersonalAccessToken()).Returns(sourceAdoPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(null, Source_Org, adoTeamProject, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, "", "", false, "", "", true, false);

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Retries_When_Hosts_Error()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.SetupSequence(x => x.GetMigrationState(migrationId).Result).Returns("FAILED").Returns("SUCCEEDED");
            mockGithub.Setup(x => x.GetMigrationFailureReason(migrationId).Result).Returns("Warning: Permanently added XXXXX (ECDSA) to the list of known hosts");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, "");

            mockGithub.Verify(x => x.DeleteRepo(Target_Org, Target_Repo));
            mockGithub.Verify(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, "", ""), Times.Exactly(2));
        }

        [Fact]
        public async Task Only_Retries_Once()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("FAILED");
            mockGithub.Setup(x => x.GetMigrationFailureReason(migrationId).Result).Returns("Warning: Permanently added XXXXX (ECDSA) to the list of known hosts");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, ""))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task No_Source_Provided_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, null, null, Source_Repo, Target_Org, Target_Repo, ""))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Ado_Source_Without_Team_Project_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, Source_Org, null, Source_Repo, Target_Org, Target_Repo, ""))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Defaults_Target_Repo_To_SourceRepo()
        {
            var githubOrgId = Guid.NewGuid().ToString();
            var migrationSourceId = Guid.NewGuid().ToString();
            var sourceGithubPat = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
            var migrationId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Source_Repo, "", "").Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, mockGithubApiFactory.Object, environmentVariableProviderMock.Object, null);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, null, "");

            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ghes_Without_AzureConnectionString_Throws_Error()
        {
            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, null, null, null, null);
            await FluentActions
                .Invoking(async () => await command.Invoke(null, null, null, Source_Repo, Target_Org, Target_Repo, GHES_Api_Url, ""))
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
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
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
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, authenticatedGitArchiveUrl.ToString(), authenticatedMetadataArchiveUrl.ToString()).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(Source_Org, Source_Repo).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(Source_Org, Source_Repo).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, metadataArchiveId).Result).Returns(metadataArchiveUrl);

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
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_Api_Url)).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(azureConnectionStringEnv)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, GHES_Api_Url, "");

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
            var githubRepoUrl = $"https://github.com/{Source_Org}/{Source_Repo}";
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
            mockGithub.Setup(x => x.GetOrganizationId(Target_Org).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.CreateGhecMigrationSource(githubOrgId, sourceGithubPat, targetGithubPat, false).Result).Returns(migrationSourceId);
            mockGithub.Setup(x => x.StartMigration(migrationSourceId, githubRepoUrl, githubOrgId, Target_Repo, authenticatedGitArchiveUrl.ToString(), authenticatedMetadataArchiveUrl.ToString()).Result).Returns(migrationId);
            mockGithub.Setup(x => x.GetMigrationState(migrationId).Result).Returns("SUCCEEDED");

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(Source_Org, Source_Repo).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(Source_Org, Source_Repo).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, metadataArchiveId).Result).Returns(ArchiveMigrationStatus.Exported);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, gitArchiveId).Result).Returns(gitArchiveUrl);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationUrl(Source_Org, metadataArchiveId).Result).Returns(metadataArchiveUrl);

            var mockAzureApi = new Mock<AzureApi>(null, null);
            mockAzureApi.Setup(x => x.DownloadArchive(gitArchiveUrl).Result).Returns(gitArchiveContent);
            mockAzureApi.Setup(x => x.DownloadArchive(metadataArchiveUrl).Result).Returns(metadataArchiveContent);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), gitArchiveContent).Result).Returns(authenticatedGitArchiveUrl);
            mockAzureApi.Setup(x => x.UploadToBlob(It.IsAny<string>(), metadataArchiveContent).Result).Returns(authenticatedMetadataArchiveUrl);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.SourceGithubPersonalAccessToken()).Returns(sourceGithubPat);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.CreateClientNoSsl(GHES_Api_Url)).Returns(mockGhesGithubApi.Object);

            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(Target_Api_Url)).Returns(mockGithub.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.CreateClientNoSsl(Azure_Connection_String)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, mockTargetGithubApiFactory.Object, environmentVariableProviderMock.Object, mockAzureApiFactory.Object);
            await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, GHES_Api_Url, Azure_Connection_String, true);

            mockAzureApiFactory.Verify(x => x.CreateClientNoSsl(Azure_Connection_String));
            mockGithub.Verify(x => x.GetMigrationState(migrationId));
        }

        [Fact]
        public async Task Ghes_Failed_Archive_Generation_Throws_Error()
        {
            var gitArchiveId = 1;
            var metadataArchiveId = 2;

            var mockGhesGithubApi = new Mock<GithubApi>(null, null);
            mockGhesGithubApi.Setup(x => x.StartGitArchiveGeneration(Source_Org, Source_Repo).Result).Returns(gitArchiveId);
            mockGhesGithubApi.Setup(x => x.StartMetadataArchiveGeneration(Source_Org, Source_Repo).Result).Returns(metadataArchiveId);
            mockGhesGithubApi.Setup(x => x.GetArchiveMigrationStatus(Source_Org, gitArchiveId).Result).Returns(ArchiveMigrationStatus.Failed);

            var mockAzureApi = new Mock<AzureApi>(null, null);

            var mockSourceGithubApiFactory = new Mock<ISourceGithubApiFactory>();
            mockSourceGithubApiFactory.Setup(m => m.Create(GHES_Api_Url)).Returns(mockGhesGithubApi.Object);

            var mockAzureApiFactory = new Mock<IAzureApiFactory>();
            mockAzureApiFactory.Setup(m => m.Create(Azure_Connection_String)).Returns(mockAzureApi.Object);

            var command = new MigrateRepoCommand(new Mock<OctoLogger>().Object, mockSourceGithubApiFactory.Object, null, null, mockAzureApiFactory.Object);
            await FluentActions
                .Invoking(async () => await command.Invoke(Source_Org, null, null, Source_Repo, Target_Org, Target_Repo, Target_Api_Url, GHES_Api_Url, Azure_Connection_String))
                .Should().ThrowAsync<OctoshiftCliException>();
        }
    }
}
