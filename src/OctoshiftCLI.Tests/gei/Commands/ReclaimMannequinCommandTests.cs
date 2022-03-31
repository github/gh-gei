using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;
using OctoshiftCLI.Models;
using Xunit;


namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands
{
    public class ReclaimMannequinCommandTests
    {
        private const string TARGET_API_URL = "https://api.github.com";

        [Fact]
        public void Should_Have_Options()
        {
            var command = new ReclaimMannequinCommand(null, null, null);
            Assert.NotNull(command);
            Assert.Equal("reclaim-mannequin", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "mannequin-user", true);
            TestHelpers.VerifyCommandOption(command.Options, "target-user", true);
            TestHelpers.VerifyCommandOption(command.Options, "force", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona"
            };

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(true);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);
            var reclaimed = await command.Invoke(githubOrg, mannequinUser, targetUser, false);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId));
            reclaimed.Should().BeTrue();
        }

        [Fact]
        public async Task AlreadyMapped_No_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = "mona",
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            };

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);


            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
        }

        [Fact]
        public async Task AlreadyMapped_Force_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();

            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = mannequinUser,
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            };

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(true);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);
            var reclaimed = await command.Invoke(githubOrg, mannequinUser, targetUser, true);

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId));
            reclaimed.Should().BeTrue();
        }

        [Fact]
        public async Task NoExistantMannequin_No_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "monadoesnotexist";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var mannequinResponse = new Mannequin();

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);
            mockGithub.Setup(x => x.GetUserId(targetUser).Result).Returns(targetUserId);
            mockGithub.Setup(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId).Result).Returns(true);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }

        [Fact]
        public async Task NoTargetUser_No_Reclaim()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var targetGithubPat = Guid.NewGuid().ToString();
            var mannequinResponse = new Mannequin
            {
                Id = mannequinUserId,
                Login = mannequinUser,
                MappedUser = new Claimant
                {
                    Id = "claimantId",
                    Login = "claimant"
                }
            };

            var mockGithub = new Mock<GithubApi>(null, null);
            mockGithub.Setup(x => x.GetOrganizationId(githubOrg).Result).Returns(githubOrgId);
            mockGithub.Setup(x => x.GetMannequin(githubOrgId, mannequinUser).Result).Returns(mannequinResponse);

            var environmentVariableProviderMock = new Mock<EnvironmentVariableProvider>(null);
            environmentVariableProviderMock.Setup(m => m.TargetGithubPersonalAccessToken()).Returns(targetGithubPat);

            var mockGithubApiFactory = new Mock<ITargetGithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(TARGET_API_URL)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(new Mock<OctoLogger>().Object, mockGithubApiFactory.Object, environmentVariableProviderMock.Object);

            await FluentActions
                .Invoking(async () => await command.Invoke(githubOrg, mannequinUser, targetUser, false))
                .Should().ThrowAsync<OctoshiftCliException>();

            mockGithub.Verify(x => x.ReclaimMannequin(githubOrgId, mannequinUserId, targetUserId), Times.Never());
            mockGithub.Verify(x => x.GetUserId(targetUser), Times.Never());
        }
    }
}
