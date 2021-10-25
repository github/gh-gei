using Moq;
using OctoshiftCLI.Commands;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Commands
{
    [Collection("Sequential")]
    public class RewirePipelineCommandTests
    {
        [Fact]
        public void ShouldHaveOptions()
        {
            var command = new RewirePipelineCommand();
            Assert.NotNull(command);
            Assert.Equal("rewire-pipeline", command.Name);
            Assert.Equal(6, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "service-connection-id", true);
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
            var adoToken = Guid.NewGuid().ToString();
            var pipelineId = 1234;
            var pipeline = new AdoPipeline();

            var mockAdo = new Mock<AdoApi>(string.Empty);
            mockAdo.Setup(x => x.GetPipelineId(adoOrg, adoTeamProject, adoPipeline).Result).Returns(pipelineId);
            mockAdo.Setup(x => x.GetPipeline(adoOrg, adoTeamProject, pipelineId).Result).Returns(pipeline);

            Environment.SetEnvironmentVariable("ADO_PAT", adoToken);
            AdoApiFactory.Create = token => token == adoToken ? mockAdo.Object : null;

            var command = new RewirePipelineCommand();
            await command.Invoke(adoOrg, adoTeamProject, adoPipeline, githubOrg, githubRepo, serviceConnectionId);

            mockAdo.Verify(x => x.ChangePipelineRepo(pipeline, githubOrg, githubRepo, serviceConnectionId));
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
