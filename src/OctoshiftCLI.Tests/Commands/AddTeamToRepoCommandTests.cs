using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class AddTeamToRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new AddTeamToRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "team", true);
            TestHelpers.VerifyCommandOption(command.Options, "role", true);
        }

        [Fact]
        public async Task HappyPath()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var team = "foo-team";
            var role = "maintain";
            var githubToken = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(string.Empty);

            Environment.SetEnvironmentVariable("GH_PAT", githubToken);
            GithubApiFactory.Create = token => token == githubToken ? mockGithub.Object : null;

            var command = new AddTeamToRepoCommand();
            await command.Invoke(githubOrg, githubRepo, team, role);

            mockGithub.Verify(x => x.AddTeamToRepo(githubOrg, githubRepo, team, role));
        }

        [Fact]
        public async Task MissingGithubPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", string.Empty);

            var command = new AddTeamToRepoCommand();

            using var console = new ConsoleOutput();
            await command.Invoke("foo", "foo", "foo", "foo");
            Assert.Contains("ERROR: NO GH_PAT FOUND", console.GetOuput(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
