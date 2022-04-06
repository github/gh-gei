using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class CreateTeamCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new CreateTeamCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("create-team", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "team-name", true);
            TestHelpers.VerifyCommandOption(command.Options, "idp-group", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var teamName = "foo-team";
            var idpGroup = "foo-group";
            var teamMembers = new List<string>() { "dylan", "dave" };
            var idpGroupId = 42;
            var teamSlug = "foo-slug";

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetTeamMembers(githubOrg, teamName).Result).Returns(teamMembers);
            mockGithub.Setup(x => x.GetIdpGroupId(githubOrg, idpGroup).Result).Returns(idpGroupId);
            mockGithub.Setup(x => x.GetTeamSlug(githubOrg, teamName).Result).Returns(teamSlug);
            mockGithub.Setup(x => x.GetTeams(githubOrg).Result).Returns(new List<string>());

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new CreateTeamCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, teamName, idpGroup);

            mockGithub.Verify(x => x.CreateTeam(githubOrg, teamName));
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[0]));
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[1]));
            mockGithub.Verify(x => x.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            var mockGithub = new Mock<GithubApi>(null, null, null);
            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(mockGithub.Object);

            var command = new CreateTeamCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke("githubOrg", "teamName", "idpGroup", githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Idempotency_Team_Exists()
        {
            var githubOrg = "FooOrg";
            var teamName = "foo-team";
            var idpGroup = "foo-group";
            var teamMembers = new List<string>() { "dylan", "dave" };
            var idpGroupId = 42;
            var teamSlug = "foo-slug";

            var mockGithub = new Mock<GithubApi>(null, null, null);
            mockGithub.Setup(x => x.GetTeamMembers(githubOrg, teamName).Result).Returns(teamMembers);
            mockGithub.Setup(x => x.GetIdpGroupId(githubOrg, idpGroup).Result).Returns(idpGroupId);
            mockGithub.Setup(x => x.GetTeamSlug(githubOrg, teamName).Result).Returns(teamSlug);
            mockGithub.Setup(x => x.GetTeams(githubOrg).Result).Returns(new List<string> { teamName });

            var mockGithubApiFactory = new Mock<GithubApiFactory>(null, null, null, null, null);
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));
            mockLogger.Setup(m => m.LogSuccess(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            var command = new CreateTeamCommand(mockLogger.Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, teamName, idpGroup);

            mockGithub.Verify(x => x.CreateTeam(githubOrg, teamName), Times.Never);
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[0]));
            mockGithub.Verify(x => x.RemoveTeamMember(githubOrg, teamName, teamMembers[1]));
            mockGithub.Verify(x => x.AddEmuGroupToTeam(githubOrg, teamSlug, idpGroupId));
            actualLogOutput.Contains($"Team '{teamName}' already exists. New team will not be created");
        }
    }
}
