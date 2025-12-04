using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.RewirePipeline;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.RewirePipeline
{
    public class RewirePipelineCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoPipelineTriggerServiceFactory> _mockAdoPipelineTriggerServiceFactory = TestHelpers.CreateMock<AdoPipelineTriggerServiceFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly RewirePipelineCommand _command = [];

        public RewirePipelineCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object)
                .AddSingleton(_mockAdoPipelineTriggerServiceFactory.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("rewire-pipeline", _command.Name);
            Assert.Equal(12, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pipeline", false); // Made optional when ID is provided
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pipeline-id", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(_command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(_command.Options, "dry-run", false);
            TestHelpers.VerifyCommandOption(_command.Options, "monitor-timeout-minutes", false);
        }

        [Fact]
        public void It_Uses_The_Ado_Pat_When_Provided()
        {
            var adoPat = "abc123";

            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = "foo-org",
                AdoTeamProject = "blah-tp",
                AdoPipeline = "some-pipeline",
                GithubOrg = "gh-org",
                GithubRepo = "gh-repo",
                ServiceConnectionId = Guid.NewGuid().ToString(),
                AdoPat = adoPat,
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }

        [Fact]
        public void It_Accepts_Pipeline_Id_Instead_Of_Pipeline_Name()
        {
            var args = new RewirePipelineCommandArgs
            {
                AdoOrg = "foo-org",
                AdoTeamProject = "blah-tp",
                AdoPipelineId = 123,
                GithubOrg = "gh-org",
                GithubRepo = "gh-repo",
                ServiceConnectionId = Guid.NewGuid().ToString(),
            };

            var handler = _command.BuildHandler(args, _serviceProvider);

            Assert.NotNull(handler);
        }
    }
}
