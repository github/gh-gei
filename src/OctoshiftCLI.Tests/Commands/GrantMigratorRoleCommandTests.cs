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
            var githubToken = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(string.Empty);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);

            Environment.SetEnvironmentVariable("GH_PAT", githubToken);
            GithubApiFactory.Create = token => token == githubToken ? mockGithub.Object : null;

            var command = new GrantMigratorRoleCommand();
            await command.Invoke(githubOrg, actor, actorType);

            mockGithub.Verify(x => x.GrantMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task MissingGHPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", string.Empty);

            var command = new GrantMigratorRoleCommand();

            await command.Invoke("foo", "foo", "TEAM");
        }

        [Fact]
        public async Task InvalidActorType()
        {
            GithubApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("GH_PAT", Guid.NewGuid().ToString());

            var command = new GrantMigratorRoleCommand();

            await command.Invoke("foo", "foo", "foo");
        }
    }
}