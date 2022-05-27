using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class GrantMigratorRoleCommandTests
    {
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly GrantMigratorRoleCommand _command;

        public GrantMigratorRoleCommandTests()
        {
            _command = new GrantMigratorRoleCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("grant-migrator-role", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
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
            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(_mockGithubApi.Object);

            await _command.Invoke(githubOrg, actor, actorType);

            _mockGithubApi.Verify(x => x.GrantMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task Invalid_Actor_Type()
        {
            await _command.Invoke("foo", "foo", "foo");
        }

        [Fact]
        public async Task It_Uses_Github_Target_Pat_When_Provided()
        {
            // Arrange
            const string githubTargetPat = "github-target-pat";

            _mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubTargetPat)).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            await _command.Invoke("githubOrg", "actor", "TEAM", githubTargetPat);

            // Assert
            actualLogOutput.Should().Contain("GITHUB TARGET PAT: ***");
            _mockTargetGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
        }
    }
}
