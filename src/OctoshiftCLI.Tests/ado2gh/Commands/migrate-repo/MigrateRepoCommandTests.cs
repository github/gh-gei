using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Contracts;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands
{
    public class MigrateRepoCommandTests
    {
        private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly ServiceProvider _serviceProvider;
        private readonly MigrateRepoCommand _command = new();

        public MigrateRepoCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockGithubApiFactory.Object)
                .AddSingleton(_mockEnvironmentVariableProvider.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("migrate-repo");
            _command.Options.Count.Should().Be(9);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "wait", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Uses_Github_Pat_When_Provided()
        {
            var adoPat = "abc123";
            var githubPat = "def456";

            var args = new MigrateRepoCommandArgs
            {
                AdoOrg = "foo-org",
                AdoTeamProject = "blah-tp",
                AdoRepo = "some-repo",
                GithubOrg = "gh-org",
                GithubRepo = "gh-repo",
                Wait = true,
                AdoPat = adoPat,
                GithubPat = githubPat,
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(null, githubPat));
        }
    }
}
