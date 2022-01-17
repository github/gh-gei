using System.Threading.Tasks;
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

            var mockGithub = new Mock<GithubApi>(null);
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create()).Returns(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, adoOrg, adoTeamProject));
        }
    }
}