using System;
using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class RewirePipelineCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new RewirePipelineCommand(null, null);
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(7, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public async Task HappyPath()
        {
            var adoOrg = "FooOrg";
            var adoTeamProject = "BlahTeamProject";
            var adoPipeline = "foo-pipeline";
            var githubOrg = "foo-gh-org";
            var githubRepo = "foo-repo";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var pipeline = new AdoPipeline();

            var mockAdo = new Mock<AdoApi>(null);
            mockAdo.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            mockAdo.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns(pipeline);

            using var adoFactory = new AdoApiFactory(mockAdo.Object);

            var command = new RewirePipelineCommand(new OctoLogger(), adoFactory);
            await command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId);

            mockAdo.Verify(x => x.ChangePipelineRepo(pipeline, githubOrg, githubRepo, serviceConnectionId));
        }
    }
}