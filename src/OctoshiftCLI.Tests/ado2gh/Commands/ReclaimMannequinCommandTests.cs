using System;
using System.Threading.Tasks;
using Moq;
using Octoshift.Services;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class ReclaimMannequinCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new ReclaimMannequinCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("reclaim-mannequin", command.Name);
            Assert.Equal(8, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "csv", false);
            TestHelpers.VerifyCommandOption(command.Options, "mannequin-user", false);
            TestHelpers.VerifyCommandOption(command.Options, "mannequin-id", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-user", false);
            TestHelpers.VerifyCommandOption(command.Options, "force", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task It_Uses_The_Github_Pat_When_Provided()
        {
            var githubOrg = "FooOrg";
            var githubOrgId = Guid.NewGuid().ToString();
            var mannequinUser = "mona";
            var mannequinUserId = Guid.NewGuid().ToString();
            var targetUser = "mona_emu";
            var targetUserId = Guid.NewGuid().ToString();
            var githubPat = "PAT";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();

            var reclaimServiceMock = TestHelpers.CreateMock<ReclaimService>();
            reclaimServiceMock.Setup(x => x.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false)).Returns(Task.FromResult(default(object)));

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, githubPat)).Returns(mockGithub.Object);

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, reclaimServiceMock.Object);
            await command.Invoke(githubOrg, null, mannequinUser, mannequinUserId, targetUser, false, githubPat);

            mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }

        [Fact]
        public async Task SingleReclaiming_Happy_Path()
        {
            var githubOrg = "FooOrg";
            var mannequinUser = "mona";
            string mannequinUserId = null;
            var targetUser = "mona_emu";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var reclaimServiceMock = TestHelpers.CreateMock<ReclaimService>();
            reclaimServiceMock.Setup(x => x.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false)).Returns(Task.FromResult(default(object)));

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, reclaimServiceMock.Object);
            await command.Invoke(githubOrg, null, mannequinUser, mannequinUserId, targetUser, false);

            reclaimServiceMock.Verify(x => x.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false), Times.Once);
        }

        [Fact]
        public async Task SingleReclaiming_WithIDSpecified_Happy_Path()
        {
            var githubOrg = "FooOrg";
            var mannequinUser = "mona";
            var mannequinUserId = "monaid";
            var targetUser = "mona_emu";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var reclaimServiceMock = TestHelpers.CreateMock<ReclaimService>();
            reclaimServiceMock.Setup(x => x.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false)).Returns(Task.FromResult(default(object)));

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, reclaimServiceMock.Object);
            await command.Invoke(githubOrg, null, mannequinUser, mannequinUserId, targetUser, false);

            reclaimServiceMock.Verify(x => x.ReclaimMannequin(mannequinUser, mannequinUserId, targetUser, githubOrg, false), Times.Once);
        }

        [Fact]
        public async Task CSVReclaiming_Happy_Path()
        {
            var githubOrg = "FooOrg";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var reclaimServiceMock = TestHelpers.CreateMock<ReclaimService>();
            reclaimServiceMock.Setup(x => x.ReclaimMannequins(Array.Empty<string>(), githubOrg, false)).Returns(Task.FromResult(default(object)));

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, reclaimServiceMock.Object)
            {
                FileExists = (s) => true,
                GetFileContent = (s) => Array.Empty<string>()
            };
            await command.Invoke(githubOrg, "file.csv", null, null, null, false);

            reclaimServiceMock.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), githubOrg, false), Times.Once);
        }

        [Fact]
        public async Task CSV_CSV_TakesPrecedence()
        {
            var githubOrg = "FooOrg";
            var mannequinUser = "mona";
            var targetUser = "mona_emu";

            var mockGithub = TestHelpers.CreateMock<GithubApi>();

            var mockGithubApiFactory = TestHelpers.CreateMock<GithubApiFactory>();
            mockGithubApiFactory.Setup(m => m.Create(null, null)).Returns(mockGithub.Object);

            var reclaimServiceMock = TestHelpers.CreateMock<ReclaimService>();
            reclaimServiceMock.Setup(x => x.ReclaimMannequin(mannequinUser, null, targetUser, githubOrg, false)).Returns(Task.FromResult(default(object)));

            var command = new ReclaimMannequinCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockGithubApiFactory.Object, reclaimServiceMock.Object)
            {
                FileExists = (s) => true,
                GetFileContent = (s) => Array.Empty<string>()
            };
            await command.Invoke(githubOrg, "file.csv", mannequinUser, null, targetUser, false); // All parameters passed. CSV has precedence

            reclaimServiceMock.Verify(x => x.ReclaimMannequins(Array.Empty<string>(), githubOrg, false), Times.Once);
        }
    }
}
