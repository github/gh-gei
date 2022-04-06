using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class DisableRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new DisableRepoCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("disable-ado-repo", command.Name);
            Assert.Equal(5, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoRepo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();
            var repos = new List<(string Id, string Name, bool IsDisabled)> { (repoId, adoRepo, false) };

            var mockAdo = new Mock<AdoApi>();
            mockAdo.Setup(x => x.GetRepos(adoOrg, adoTeamProject).Result).Returns(repos);

            var mockAdoApiFactory = new Mock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdo.Object);

            var command = new DisableRepoCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.DisableRepo(adoOrg, adoTeamProject, repoId));
        }

        [Fact]
        public async Task Idempotency_Repo_Disabled()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "TeamProject1";
            var adoRepo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();
            var repos = new List<(string Id, string Name, bool IsDisabled)> { (repoId, adoRepo, true) };

            var mockAdo = new Mock<AdoApi>();
            mockAdo.Setup(x => x.GetRepos(adoOrg, adoTeamProject).Result).Returns(repos);

            var mockAdoApiFactory = new Mock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdo.Object);

            var command = new DisableRepoCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.DisableRepo(adoOrg, adoTeamProject, repoId), Times.Never);
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var repos = new[] { ("repoId", "adoRepo", true) };
            var mockAdo = new Mock<AdoApi>();
            mockAdo.Setup(x => x.GetRepos(It.IsAny<string>(), It.IsAny<string>()).Result).Returns(repos);
            var mockAdoApiFactory = new Mock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new DisableRepoCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "adoRepo", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
