using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class RewirePipelineCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly RewirePipelineCommand _command = new();

        public RewirePipelineCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("rewire-pipeline", _command.Name);
            Assert.Equal(8, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pipeline", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "service-connection-id", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
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
    }
}
