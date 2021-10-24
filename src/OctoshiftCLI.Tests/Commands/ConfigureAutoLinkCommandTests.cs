using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class ConfigureAutoLinkCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new ConfigureAutoLinkCommand();
            Assert.NotNull(command);
            Assert.Equal("configure-autolink", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
        }

        [Fact]
        public async Task HappyPath()
        {
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var adoOrg = "foo-ado-org";
            var adoTeamProject = "foo-ado-tp";
            var githubToken = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(string.Empty);

            Environment.SetEnvironmentVariable("GH_PAT", githubToken);
            GithubApiFactory.Create = token => token == githubToken ? mockGithub.Object : null;

            var command = new ConfigureAutoLinkCommand();
            await command.Invoke(githubOrg, githubRepo, adoOrg, adoTeamProject);

            mockGithub.Verify(x => x.AddAutoLink(githubOrg, githubRepo, adoOrg, adoTeamProject));
        }

        [Fact]
        public async Task MissingGithubPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", string.Empty);

            var command = new ConfigureAutoLinkCommand();

            await command.Invoke("foo", "foo", "foo", "foo");
        }
    }
}
