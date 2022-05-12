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
    public class DownloadLogsCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new DownloadLogsCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("download-logs", command.Name);
            Assert.Equal(7, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "migration-log-file", false);
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
            mockGithubApi.Setup(m => m.GetMigrationLogUrl(githubOrg, repo)).ReturnsAsync(logUrl);

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
        public async Task File_Already_Exists_No_Overwrite_Flag_Should_Throw_OctoshiftCliException()
        {
            // Arrange
            var githubOrg = "FooOrg";
            var repo = "foo-repo";
            var defaultFileName = $"migration-log-{githubOrg}-{repo}.log";

            // Act
            var command = new DownloadLogsCommand(TestHelpers.CreateMock<OctoLogger>().Object, null, null)
            {
                FileExists = _ => true
            };

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, repo))
                .Should().ThrowAsync<OctoshiftCliException>();
        }
    }
}
