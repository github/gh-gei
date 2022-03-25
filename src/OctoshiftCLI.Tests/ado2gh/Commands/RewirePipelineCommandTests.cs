using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class RewirePipelineCommandTests
    {
        [Fact]
        public void Should_Have_Options()
        {
            var command = new RewirePipelineCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(8, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task Happy_Path()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoPipeline = "foo-pipeline";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var defaultBranch = "default-branch";
            var clean = "true";
            var checkoutSubmodules = "null";

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            mockAdo.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns((defaultBranch, clean, checkoutSubmodules));

            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockAdo.Object);

            var command = new RewirePipelineCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId);

            mockAdo.Verify(x => x.ChangePipelineRepo(adoOrg, adoTeamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId));
        }

        [Fact]
        public async Task It_Uses_The_Ado_Pat_When_Provided()
        {
            const string adoPat = "ado-pat";

            var mockAdo = new Mock<AdoApi>(null);
            var mockAdoApiFactory = new Mock<AdoApiFactory>(null, null, null);
            mockAdoApiFactory.Setup(m => m.Create(adoPat)).Returns(mockAdo.Object);

            var command = new RewirePipelineCommand(new Mock<OctoLogger>().Object, mockAdoApiFactory.Object);
            await command.Invoke("adoOrg", "adoTeamProject", "adoPipeline", "githubOrg", "githubRepo", "serviceConnectionId", adoPat);

            mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
