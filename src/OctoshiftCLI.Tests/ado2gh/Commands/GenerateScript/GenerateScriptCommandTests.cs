using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.GenerateScript;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.GenerateScript
{
    public class GenerateScriptCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<IVersionProvider> _mockVersionProvider = new();

        private readonly ServiceProvider _serviceProvider;
        private readonly GenerateScriptCommand _command = [];

        public GenerateScriptCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object)
                .AddSingleton(_mockAdoInspectorServiceFactory.Object)
                .AddSingleton(_mockVersionProvider.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(17);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-team-project", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-server-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "download-migration-logs", false);
            TestHelpers.VerifyCommandOption(command.Options, "create-teams", false);
            TestHelpers.VerifyCommandOption(command.Options, "link-idp-groups", false);
            TestHelpers.VerifyCommandOption(command.Options, "lock-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "disable-ado-repos", false);
            TestHelpers.VerifyCommandOption(command.Options, "rewire-pipelines", false);
            TestHelpers.VerifyCommandOption(command.Options, "all", false);
            TestHelpers.VerifyCommandOption(command.Options, "repo-list", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        }

        [Fact]
        public void It_Uses_The_Ado_Pat_When_Provided()
        {
            // Arrange
            var adoPat = "ado-pat";

            // Act
            var args = new GenerateScriptCommandArgs
            {
                GithubOrg = "foo-org",
                AdoOrg = "blah-ado-org",
                AdoPat = adoPat,
                Output = new FileInfo("unit-test-output")
            };

            _command.BuildHandler(args, _serviceProvider);

            // Assert
            _mockAdoApiFactory.Verify(m => m.Create(null, adoPat));
        }
    }
}
