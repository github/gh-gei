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
    public class RevokeMigratorRoleCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new RevokeMigratorRoleCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("revoke-migrator-role", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var actor = "foo-actor";
            var actorType = "TEAM";
            var githubOrgId = Guid.NewGuid().ToString();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Returns(mockGithub.Object);

            var command = new RevokeMigratorRoleCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object);
            await command.Invoke(githubOrg, actor, actorType);

            mockGithub.Verify(x => x.RevokeMigratorRole(githubOrgId, actor, actorType));
        }

        [Fact]
        public async Task Invalid_Actor_Type()
        {
            var command = new RevokeMigratorRoleCommand(new Mock<OctoLogger>().Object, null);

            await command.Invoke("foo", "foo", "foo");
        }

        [Fact]
        public async Task It_Uses_Github_Target_Pat_When_Provided()
        {
            // Arrange
            const string githubTargetPat = "github-target-pat";

            var mockGithub = new Mock<GithubApi>(null, null);
            var mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockTargetGithubApiFactory.Setup(m => m.Create(It.IsAny<string>(), githubTargetPat)).Returns(mockGithub.Object);

            var actualLogOutput = new List<string>();
            var mockLogger = new Mock<OctoLogger>();
            mockLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            var command = new RevokeMigratorRoleCommand(mockLogger.Object, mockTargetGithubApiFactory.Object);
            await command.Invoke("githubOrg", "actor", "TEAM", githubTargetPat);

            // Assert
            actualLogOutput.Should().Contain("GITHUB TARGET PAT: ***");
            mockTargetGithubApiFactory.Verify(m => m.Create(null, githubTargetPat));
        }
    }
}
