using System.CommandLine;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class AddTeamToRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new AddTeamToRepoCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "team", true);
            TestHelpers.VerifyCommandOption(command.Options, "role", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var team = "foo-team";
            var role = "maintain";

            var mockGithub = new Mock<GithubApi>(null);

            using var githubFactory = new GithubApiFactory(mockGithub.Object);
            var command = new AddTeamToRepoCommand(new Mock<OctoLogger>().Object, githubFactory);
            await command.Invoke(githubOrg, githubRepo, team, role);

            mockGithub.Verify(x => x.AddTeamToRepo(githubOrg, githubRepo, team, role));
        }

        [Fact]
        public async Task InvalidRole()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var team = "foo-team";
            var role = "read";  // read is not a valid role

            var mockGithub = new Mock<GithubApi>(null);

            using var githubFactory = new GithubApiFactory(mockGithub.Object);
            var command = new AddTeamToRepoCommand(new Mock<OctoLogger>().Object, githubFactory);

            var root = new RootCommand();
            root.AddCommand(command);
            var args = new string[] { "add-team-to-repo", "--github-org", githubOrg, "--github-repo", githubRepo, "--team", team, "--role", role };
            await root.InvokeAsync(args);

            mockGithub.Verify(x => x.AddTeamToRepo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}