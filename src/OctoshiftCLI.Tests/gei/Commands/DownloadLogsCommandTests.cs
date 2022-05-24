using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class DownloadLogsCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new DownloadLogsCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("download-logs", command.Name);
            Assert.Equal(8, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "migration-log-file", false);
            TestHelpers.VerifyCommandOption(command.Options, "timeout-minutes", false);
            TestHelpers.VerifyCommandOption(command.Options, "overwrite", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";
            var defaultFileName = $"migration-log-{githubOrg}-{repo}.log";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object);
            await command.Invoke(githubOrg, repo);

            // Assert
            mockHttpDownloadService.Verify(m => m.Download(logUrl, defaultFileName));
        }

        [Fact]
        public async Task Calls_GetMigrationLogUrl_With_Expected_Org_And_Repo()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object);
            await command.Invoke(githubOrg, repo);

            // Assert
            mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, repo));
        }

        [Fact]
        public async Task Calls_ITargetGithubApiFactory_With_Expected_Target_API_URL()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";
            var targetApiUrl = "api-url";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object);
            await command.Invoke(githubOrg, repo, targetApiUrl);

            // Assert
            mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, null));
        }

        [Fact]
        public async Task Calls_ITargetGithubApiFactory_With_Expected_Target_GitHub_PAT()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";
            var githubTargetPat = "github-target-pat";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, It.IsAny<string>())).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object);
            await command.Invoke(githubOrg, repo, null, githubTargetPat);

            // Assert
            mockGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
        }

        [Fact]
        public async Task Calls_Download_With_Expected_Migration_Log_File()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";
            var migrationLogFile = "migration-log-file";

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object);
            await command.Invoke(githubOrg, repo, null, null, migrationLogFile);

            // Assert
            mockHttpDownloadService.Verify(m => m.Download(It.IsAny<string>(), migrationLogFile));
        }

        [Fact]
        public async Task Waits_For_URL_To_Populate()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrlEmpty = "";
            var logUrlPopulated = "some-url";
            var defaultFileName = $"migration-log-{githubOrg}-{repo}.log";
            const int waitIntervalInSeconds = 1;

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.SetupSequence(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(logUrlEmpty)
                .ReturnsAsync(logUrlEmpty)
                .ReturnsAsync(logUrlPopulated);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            var actualLogOutput = new List<string>();
            var mockLogger = TestHelpers.CreateMock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var expectedLogOutput = new List<string>
            {
                $"Downloading logs for organization {githubOrg}...",
                $"GITHUB TARGET ORG: {githubOrg}",
                $"TARGET REPO: {repo}",
                $"Waiting {waitIntervalInSeconds} more seconds for log to populate...",
                $"Waiting {waitIntervalInSeconds} more seconds for log to populate...",
                $"Downloading log for repository {repo} to {defaultFileName}...",
                $"Downloaded {repo} log to {defaultFileName}."
            };

            // Act
            var command = new DownloadLogsCommand(mockLogger.Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object)
            {
                WaitIntervalInSeconds = waitIntervalInSeconds
            };

            await command.Invoke(githubOrg, repo, null, null, null);

            // Assert
            mockGithubApi.Verify(m => m.GetMigrationLogUrl(githubOrg, repo), Times.Exactly(3));
            mockHttpDownloadService.Verify(m => m.Download(logUrlPopulated, defaultFileName));

            mockLogger.Verify(m => m.LogInformation(It.IsAny<string>()), Times.Exactly(6));
            mockLogger.Verify(m => m.LogSuccess(It.IsAny<string>()), Times.Once);
            actualLogOutput.Should().Equal(expectedLogOutput);
        }

        [Fact]
        public async Task Calls_Download_When_File_Exists_AndOverwrite_Requested()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "some-url";
            var overwrite = true;

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            var mockHttpDownloadService = TestHelpers.CreateMock<HttpDownloadService>();
            mockHttpDownloadService.Setup(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, mockHttpDownloadService.Object)
            {
                FileExists = _ => true
            };

            await command.Invoke(githubOrg, repo, null, null, null, 1, overwrite);

            // Assert
            mockHttpDownloadService.Verify(m => m.Download(It.IsAny<string>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task File_Already_Exists_No_Overwrite_Flag_Should_Throw_OctoshiftCliException()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null)
            {
                FileExists = _ => true
            };

            // Assert
            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, repo))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Throw_OctoshiftCliException_When_No_Migration()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = (string)null;

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, null);

            // Assert
            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, repo))
                .Should().ThrowAsync<OctoshiftCliException>();
        }

        [Fact]
        public async Task Throw_OctoshiftCliException_When_Migration_Log_URL_Empty()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var logUrl = "";
            var timeoutMinutes = 0;  // Skip the retry logic so this test doesn't take a long time sleeping.

            var mockGithubApi = TestHelpers.CreateMock<GithubApi>();
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(logUrl);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithubApi.Object);

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, null);

            // Assert
            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, repo, null, null, null, timeoutMinutes))
                .Should().ThrowAsync<OctoshiftCliException>();
        }
    }
}
