using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class CreateTeamCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new CreateTeamCommand();
            Assert.NotNull(command);
            Assert.Equal("create-team", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "team-name", true);
            TestHelpers.VerifyCommandOption(command.Options, "idp-group", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var githubOrg = "FooOrg";
            var teamName = "foo-team";
            var idpGroup = "foo-group";
            var githubToken = Guid.NewGuid().ToString();
            var teamMembers = new List<string>() { "dylan", "dave" };
            var idpGroupId = 42;
            var teamSlug = "foo-slug";

            var mockGithub = new Mock<GithubApi>(string.Empty);
            mockGithub.Setup(x => x.GetTeamMembers(githubOrg, teamName).Result).Returns(teamMembers);
            mockGithub.Setup(x => x.GetIdpGroupId(githubOrg, idpGroup).Result).Returns(idpGroupId);
            mockGithub.Setup(x => x.GetTeamSlug(githubOrg, teamName).Result).Returns(teamSlug);

            Environment.SetEnvironmentVariable("GH_PAT", githubToken);
            GithubApiFactory.Create = token => token == githubToken ? mockGithub.Object : null;

            var command = new CreateTeamCommand();
            await command.Invoke(githubOrg, teamName, idpGroup);

            mockGithub.Verify(x => x.CreateTeam(githubOrg, teamName));
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[0]));
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[1]));
            mockGithub.Verify(x => x.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId));
        }

        [Fact]
        public async Task MissingADOPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", string.Empty);

            var command = new CreateTeamCommand();

            await command.Invoke("foo", "foo", "foo");
        }
    }
}
