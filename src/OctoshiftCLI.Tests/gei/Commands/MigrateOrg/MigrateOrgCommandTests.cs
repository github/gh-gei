using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateOrg;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.MigrateOrg
{
    public class MigrateOrgCommandTests
    {
        private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();
        private readonly Mock<WarningsCountLogger> _warningsCountLogger = TestHelpers.CreateMock<WarningsCountLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly MigrateOrgCommand _command = [];

        public MigrateOrgCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockGithubApiFactory.Object)
                .AddSingleton(_mockEnvironmentVariableProvider.Object)
                .AddSingleton(_warningsCountLogger.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new MigrateOrgCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("migrate-org");
            command.Options.Count.Should().Be(9);

            TestHelpers.VerifyCommandOption(command.Options, "github-source-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-enterprise", true);
            TestHelpers.VerifyCommandOption(command.Options, "queue-only", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-source-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "github-target-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-uploads-url", false, true);
        }

        [Fact]
        public void It_Uses_Target_Api_Url_When_Provided()
        {
            var githubSourcePat = "abc123";
            var githubTargetPat = "def456";
            var targetApiUrl = "https://api.github.com";
            var targetUploadsUrl = "https://uploads.github.com";

            var args = new MigrateOrgCommandArgs
            {
                GithubSourceOrg = "source-org",
                GithubSourcePat = githubSourcePat,
                GithubTargetOrg = "target-org",
                GithubTargetEnterprise = "target-enterprise",
                GithubTargetPat = githubTargetPat,
                TargetApiUrl = targetApiUrl,
                TargetUploadsUrl = targetUploadsUrl
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(targetApiUrl, targetUploadsUrl, githubTargetPat));
        }
    }
}
