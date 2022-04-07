using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class LockRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new LockRepoCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("lock-ado-repo", command.Name);
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
            var identityDescriptor = "foo-id";
            var teamProjectId = Guid.NewGuid().ToString();

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetRepoId(adoOrg, adoTeamProject, adoRepo).Result).Returns(repoId);
            mockAdo.Setup(x => x.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users").Result).Returns(identityDescriptor);

            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdo.Object);

            var command = new LockRepoCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var mockAdo = TestHelpers.CreateMock<AdoApi>();
            var mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new LockRepoCommand(TestHelpers.CreateMock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "adoRepo", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
