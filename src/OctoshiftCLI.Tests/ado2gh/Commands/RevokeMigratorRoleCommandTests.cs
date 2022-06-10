using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class RevokeMigratorRoleCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApiFactory> _mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly RevokeMigratorRoleCommand _command;

        public RevokeMigratorRoleCommandTests()
        {
            _command = new RevokeMigratorRoleCommand(_mockOctoLogger.Object, _mockGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("revoke-migrator-role", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var actor = "foo-actor";
            var actorType = "TEAM";
            var githubOrgId = Guid.NewGuid().ToString();

            _mockGithubApi.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            await _command.Invoke(githubOrg, actor, actorType);

            _mockGithubApi.Verify(x => x.RevokeMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            _mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubPat)).Returns(_mockGithubApi.Object);

            await _command.Invoke("githubOrg", "actor", "TEAM", githubPat);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task Invalid_Actor_Type()
        {
            await _command.Invoke("foo", "foo", "foo");
        }
    }
}
