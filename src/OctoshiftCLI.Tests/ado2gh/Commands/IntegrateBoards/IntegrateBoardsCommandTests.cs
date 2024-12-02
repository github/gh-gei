using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.IntegrateBoards;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.IntegrateBoards
{
    public class IntegrateBoardsCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<EnvironmentVariableProvider> _mockEnvironmentVariableProvider = TestHelpers.CreateMock<EnvironmentVariableProvider>();

        private readonly ServiceProvider _serviceProvider;
        private readonly IntegrateBoardsCommand _command = [];

        public IntegrateBoardsCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object)
                .AddSingleton(_mockEnvironmentVariableProvider.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("integrate-boards", _command.Name);
            Assert.Equal(7, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Uses_The_Ado_And_Github_Pats_When_Provided()
        {
            var adoPat = "ado-pat";

            var args = new IntegrateBoardsCommandArgs
            {
                AdoOrg = "foo-org",
                AdoTeamProject = "blah-tp",
                GithubOrg = "some-gh-org",
                GithubRepo = "some-gh-repo",
                AdoPat = adoPat,
                GithubPat = "abc123",
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
            _mockEnvironmentVariableProvider.Verify(m => m.TargetGithubPersonalAccessToken(It.IsAny<bool>()), Times.Never);
        }
    }
}
