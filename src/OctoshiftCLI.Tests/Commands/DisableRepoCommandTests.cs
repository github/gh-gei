using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class DisableRepoCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new DisableRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("disable-ado-repo", command.Name);
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
            var repoId = Guid.NewGuid().ToString();
            var adoToken = Guid.NewGuid().ToString();

            var mockAdo = new Mock<AdoApi>(string.Empty);
            mockAdo.Setup(x => x.GetRepoId(adoOrg, adoTeamProject, adoRepo).Result).Returns(repoId);

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            AdoApiFactory.Create = token => token == adoToken ? mockAdo.Object : null;

            var command = new DisableRepoCommand();
            await command.Invoke(adoOrg, adoTeamProject, adoRepo);

            mockAdo.Verify(x => x.DisableRepo(adoOrg, adoTeamProject, repoId));
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
