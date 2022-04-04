using System.CommandLine;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class AddTeamToRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new AddTeamToRepoCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "team", true);
            TestHelpers.VerifyCommandOption(command.Options, "role", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var team = "foo-team";
            var role = "maintain";

            var mockGithub = new Mock<GithubApi>(null, null);
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new AddTeamToRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, githubRepo, team, role);

            mockGithub.Verify(x => x.AddTeamToRepo(githubOrg, githubRepo, team, role));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            var mockGithub = new Mock<GithubApi>(null, null);
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithub.Object);

            var command = new AddTeamToRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke("githubOrg", "githubRepo", "team", "role", githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Invalid_Role()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var team = "foo-team";
            var role = "read";  // read is not a valid role

            var mockGithub = new Mock<GithubApi>(null, null);
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new AddTeamToRepoCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);

            var root = new RootCommand();
            root.AddCommand(command);
            var args = new string[] { "add-team-to-repo", "--github-org", githubOrg, "--github-repo", githubRepo, "--team", team, "--role", role };
            await root.InvokeAsync(args);

            mockGithub.Verify(x => x.AddTeamToRepo(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
