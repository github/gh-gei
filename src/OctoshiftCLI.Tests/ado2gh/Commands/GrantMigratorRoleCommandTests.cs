using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class GrantMigratorRoleCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new GrantMigratorRoleCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("grant-migrator-role", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var actor = "foo-actor";
            var actorType = "TEAM";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), "grant-migrator-role")).Returns(mockGithub.Object);

            var command = new GrantMigratorRoleCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, actor, actorType);

            mockGithub.Verify(x => x.GrantMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();
            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(githubPat, It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new GrantMigratorRoleCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke("githubOrg", "actor", "TEAM", githubPat);

            mockGithubApiFactory.Verify(m => m.Create(githubPat, It.IsAny<string>()));
        }

        [Fact]
        public async Task Invalid_Actor_Type()
        {
            var command = new GrantMigratorRoleCommand(TestHelpers.CreateMock<OctoLogger>().Object, null);

            await command.Invoke("foo", "foo", "foo");
        }
    }
}
