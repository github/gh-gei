using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
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
            var command = new AddTeamToRepoCommand(new OctoLogger(), githubFactory);
            await command.Invoke(githubOrg, githubRepo, team, role);

            mockGithub.Verify(x => x.AddTeamToRepo(githubOrg, githubRepo, team, role));
        }
    }
}