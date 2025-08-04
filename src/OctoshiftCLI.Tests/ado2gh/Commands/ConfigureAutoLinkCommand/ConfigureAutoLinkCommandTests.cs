using System;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.ConfigureAutoLink;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.ConfigureAutoLink
{
    public class ConfigureAutoLinkCommandTests
    {
        private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly ConfigureAutoLinkCommand _command = [];

        public ConfigureAutoLinkCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockGithubApiFactory.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("configure-autolink", _command.Name);
            Assert.Equal(6, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Uses_The_Github_Pat_When_Provided()
        {
            var githubPat = Guid.NewGuid().ToString();

            var args = new ConfigureAutoLinkCommandArgs
            {
                GithubOrg = "foo-org",
                GithubRepo = "foo-repo",
                AdoOrg = "my-ado-org",
                AdoTeamProject = "some-team-project",
                GithubPat = githubPat,
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(null, null, githubPat));
        }
    }
}
