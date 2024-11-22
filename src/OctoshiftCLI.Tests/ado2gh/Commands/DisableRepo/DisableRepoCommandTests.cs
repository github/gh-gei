using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.DisableRepo;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.DisableRepo
{
    public class DisableRepoCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly DisableRepoCommand _command = [];

        public DisableRepoCommandTests()
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
            Assert.Equal("disable-ado-repo", _command.Name);
            Assert.Equal(5, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-team-project", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-repo", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Uses_The_Ado_Pat_When_Provided()
        {
            var adoPat = "ado-pat";

            var args = new DisableRepoCommandArgs
            {
                AdoOrg = "foo-org",
                AdoTeamProject = "blah-tp",
                AdoRepo = "some-repo",
                AdoPat = adoPat
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
