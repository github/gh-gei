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
        private readonly Mock<GithubApi> _mockGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<ITargetGithubApiFactory> _mockTargetGithubApiFactory = new Mock<ITargetGithubApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly RevokeMigratorRoleCommand _command;

        public RevokeMigratorRoleCommandTests()
        {
            _command = new RevokeMigratorRoleCommand(_mockOctoLogger.Object, _mockTargetGithubApiFactory.Object);
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("revoke-migrator-role", _command.Name);
            Assert.Equal(6, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor", true);
            TestHelpers.VerifyCommandOption(_command.Options, "actor-type", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
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

            _mockGithubApi.Verify(x => x.RevokeMigratorRole(githubOrgId, actor, actorType));
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

        [Fact]
        public async Task It_Uses_Target_Api_Url_When_Provided()
        {
            // Arrange
            const string targetApiUrl = "github-target-pat";

            _mockTargetGithubApiFactory.Setup(m => m.Create(targetApiUrl, null)).Returns(_mockGithubApi.Object);

            var actualLogOutput = new List<string>();
            _mockOctoLogger.Setup(m => m.LogInformation(It.IsAny<string>())).Callback<string>(s => actualLogOutput.Add(s));

            // Act
            await _command.Invoke("githubOrg", "actor", "TEAM", targetApiUrl: targetApiUrl);

            // Assert
            actualLogOutput.Should().Contain($"TARGET API URL: {targetApiUrl}");
            _mockTargetGithubApiFactory.Verify(m => m.Create(targetApiUrl, null));
        }
    }
}
