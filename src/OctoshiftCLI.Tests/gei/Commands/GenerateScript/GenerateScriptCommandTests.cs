using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandTests
    {
        private readonly Mock<ISourceGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<IVersionProvider> _mockVersionProvider = new();
        private readonly Mock<GhesVersionCheckerFactory> _mockGhesVersionCheckerFactory = TestHelpers.CreateMock<GhesVersionCheckerFactory>();

        private readonly ServiceProvider _serviceProvider;
        private readonly GenerateScriptCommand _command = [];

        public GenerateScriptCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockEnvironmentVariableProvider.Object)
                .AddSingleton(_mockGithubApiFactory.Object)
                .AddSingleton(_mockVersionProvider.Object)
                .AddSingleton(_mockGhesVersionCheckerFactory.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new GenerateScriptCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("generate-script");
            command.Options.Count.Should().Be(17);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "skip-releases", false);
            TestHelpers.VerifyCommandOption(command.Options, "lock-source-repo", false);
            TestHelpers.VerifyCommandOption(command.Options, "download-migration-logs", false);
            TestHelpers.VerifyCommandOption(command.Options, "output", false);
            TestHelpers.VerifyCommandOption(command.Options, "sequential", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-bucket-name", false);
            TestHelpers.VerifyCommandOption(command.Options, "aws-region", false);
            TestHelpers.VerifyCommandOption(command.Options, "keep-archive", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-uploads-url", false, true);
            TestHelpers.VerifyCommandOption(command.Options, "use-github-storage", false, true);
        }

        [Fact]
        public void Creates_NoSsl_Client_When_NoSsl_Arg_Is_True()
        {
            var args = new GenerateScriptCommandArgs
            {
                GithubSourceOrg = "foo",
                GhesApiUrl = "https://github.contoso.com",
                TargetUploadsUrl = "https://uploads.github.com",
                NoSslVerify = true,
            };

            _ = _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.CreateClientNoSsl(args.GhesApiUrl, args.TargetUploadsUrl, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void It_Uses_Github_Source_Pat_When_Provided()
        {
            var args = new GenerateScriptCommandArgs
            {
                TargetUploadsUrl = "https://uploads.github.com",
                GithubSourceOrg = "foo",
                GithubSourcePat = "1234",
            };

            _ = _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(It.IsAny<string>(), args.TargetUploadsUrl, args.GithubSourcePat), Times.Once);
        }
    }
}
