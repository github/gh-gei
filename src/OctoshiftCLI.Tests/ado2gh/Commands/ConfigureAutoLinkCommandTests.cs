using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ConfigureAutoLinkCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ConfigureAutoLinkCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("configure-autolink", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-ado-tp";
            var keyPrefix = "AB#";
            var urlTemplate = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/".Replace(" ", "%20");

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>());
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1), Times.Never);
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>());
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke("githubOrg", "githubRepo", "adoOrg", "adoTeamProject", githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Idempotency_AutoLink_Exists()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-ado-tp";
            var keyPrefix = "AB#";
            var urlTemplate = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/".Replace(" ", "%20");

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.FromResult(new List<(int Id, string KeyPrefix, string UrlTemplate)>
                      {
                          (1, keyPrefix, urlTemplate),
                      }));
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var command = new ConfigureAutoLinkCommand(mockLogger.Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1), Times.Never);
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate), Times.Never);
            actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{keyPrefix}'. No operation will be performed");
        }

        [Fact]
        public async Task Idempotency_KeyPrefix_Exists()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-ado-tp";
            var keyPrefix = "AB#";
            var urlTemplate = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/".Replace(" ", "%20");

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .ReturnsAsync(new List<(int Id, string KeyPrefix, string UrlTemplate)>
                      {
                          (1, keyPrefix, "SomethingElse"),
                      });
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var command = new ConfigureAutoLinkCommand(mockLogger.Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1));
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate));
            actualLogOutput.Should().Contain($"Autolink reference already exists for key_prefix: '{keyPrefix}', but the url template is incorrect");
            actualLogOutput.Should().Contain($"Deleting existing Autolink reference for key_prefix: '{keyPrefix}' before creating a new Autolink reference");
        }
    }
}
