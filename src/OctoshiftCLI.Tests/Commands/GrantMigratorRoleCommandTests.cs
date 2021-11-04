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
            var command = new GrantMigratorRoleCommand();
            Assert.NotNull(command);
            Assert.Equal("grant-migrator-role", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
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

            GithubApiFactory.Create = () => mockGithub.Object;

            var command = new GrantMigratorRoleCommand();
            await command.Invoke(githubOrg, actor, actorType);

            mockGithub.Verify(x => x.GrantMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task InvalidActorType()
        {
            GithubApiFactory.Create = () => throw new InvalidOperationException();

            var command = new GrantMigratorRoleCommand();

            await command.Invoke("foo", "foo", "foo");
        }
    }
}