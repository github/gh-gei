using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class GrantMigratorRoleCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new GrantMigratorRoleCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("grant-migrator-role", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var githubOrg = "FooOrg";
            var actor = "foo-actor";
            var actorType = "TEAM";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);

            using var githubFactory = new GithubApiFactory(mockGithub.Object);

            var command = new GrantMigratorRoleCommand(new OctoLogger(), githubFactory);
            await command.Invoke(githubOrg, actor, actorType);

            mockGithub.Verify(x => x.GrantMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task InvalidActorType()
        {
            using var githubFactory = new GithubApiFactory(api: null);

            var command = new GrantMigratorRoleCommand(new OctoLogger(), githubFactory);

            await command.Invoke("foo", "foo", "foo");
        }
    }
}