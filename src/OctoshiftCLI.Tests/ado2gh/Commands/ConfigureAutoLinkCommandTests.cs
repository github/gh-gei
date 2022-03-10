using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Octoshift.Models;
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
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.FromResult(new List<AutoLink>()));
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1), Times.Never);
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate));
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.FromResult(new List<AutoLink>
                      {
                          new AutoLink{Id = 1, KeyPrefix = keyPrefix, UrlTemplate = urlTemplate},
                      }));
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1), Times.Never);
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate), Times.Never);
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

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetAutoLinks(It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.FromResult(new List<AutoLink>
                      {
                          new AutoLink{Id = 1, KeyPrefix = keyPrefix, UrlTemplate = "SomethingElse"},
                      }));
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.DeleteAutoLink(githubOrg, githubRepo, 1), Times.Once);
            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, keyPrefix, urlTemplate), Times.Once);
        }
    }
}
