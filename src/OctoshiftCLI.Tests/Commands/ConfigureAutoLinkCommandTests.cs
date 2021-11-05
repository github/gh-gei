using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class ConfigureAutoLinkCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
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
        public async Task HappyPath()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-ado-tp";

            var mockGithub = new Mock<GithubApi>(null);

            using var githubFactory = new GithubApiFactory(mockGithub.Object);

            var command = new ConfigureAutoLinkCommand(new Mock<OctoLogger>().Object, githubFactory);
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, adoOrg, adoTeamProject));
        }
    }
}