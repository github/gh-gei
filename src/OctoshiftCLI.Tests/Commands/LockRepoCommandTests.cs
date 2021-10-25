using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class LockRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new LockRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("lock-ado-repo", command.Name);
            Assert.Equal(3, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-repo", true);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoRepo = "foo-repo";
            var adoToken = Guid.NewGuid().ToString();
            var repoId = Guid.NewGuid().ToString();
            var identityDescriptor = "foo-id";
            var teamProjectId = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(string.Empty);
            mockAdo.Setup(x => x.GetTeamProjectId(adoOrg, adoTeamProject).Result).Returns(teamProjectId);
            mockAdo.Setup(x => x.GetRepoId(adoOrg, adoTeamProject, adoRepo).Result).Returns(repoId);
            mockAdo.Setup(x => x.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users").Result).Returns(identityDescriptor);

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            AdoApiFactory.Create = token => token == adoToken ? mockAdo.Object : null;

            var command = new LockRepoCommand();
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor));
        }

        [Fact]
        public async Task MissingADOPat()
        {
            // When there's no PAT it should never call the factory, forcing it to throw an exception gives us an easy way to test this
            AdoApiFactory.Create = token => throw new InvalidOperationException();
            Environment.SetEnvironmentVariable("ADO_PAT", string.Empty);

            var command = new DisableRepoCommand();

            await command.Invoke("foo", "foo", "foo");
        }
    }
}
