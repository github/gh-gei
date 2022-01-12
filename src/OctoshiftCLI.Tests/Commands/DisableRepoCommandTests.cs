using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    public class DisableRepoCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new DisableRepoCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("disable-ado-repo", command.Name);
            Assert.Equal(4, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoRepo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetRepoId(adoOrg, adoTeamProject, adoRepo).Result).Returns(repoId);

            var command = new DisableRepoCommand(new Mock<OctoLogger>().Object, new Lazy<AdoApi>(mockAdo.Object));
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.DisableRepo(adoOrg, adoTeamProject, repoId));
        }
    }
}