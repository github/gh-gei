using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.AddTeamToRepo;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.AddTeamToRepo
{
    public class AddTeamToRepoCommandTests
    {
        private readonly Mock<ITargetGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly ServiceProvider _serviceProvider;
        private readonly AddTeamToRepoCommand _command = [];

        public AddTeamToRepoCommandTests()
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
            var command = new AddTeamToRepoCommand();
            Assert.NotNull(command);
            Assert.Equal("add-team-to-repo", command.Name);
            Assert.Equal(7, command.Options.Count);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-repo", true);
            TestHelpers.VerifyCommandOption(command.Options, "team", true);
            TestHelpers.VerifyCommandOption(command.Options, "role", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(command.Options, "target-api-url", false);
        }

        [Fact]
        public void It_Uses_The_Github_Pat_When_Provided()
        {
            const string githubPat = "github-pat";

            var args = new AddTeamToRepoCommandArgs
            {
                GithubOrg = "foo",
                GithubRepo = "blah",
                Team = "some-team",
                Role = "role",
                GithubPat = githubPat
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(null, null, githubPat));
        }

        [Fact]
        public async Task Invalid_Role()
        {
            var role = "read";  // read is not a valid role

            var args = new string[] { "add-team-to-repo", "--github-org", "foo-org", "--github-repo", "blah-repo", "--team", "some-team", "--role", role };
            var command = new AddTeamToRepoCommand();
            var argsBinder = new GenericArgsBinder<AddTeamToRepoCommand, AddTeamToRepoCommandArgs>(command);
            command.SetHandler(async x => await command.BuildHandler(x, _serviceProvider).Handle(x), argsBinder);
            await command.InvokeAsync(args);

            _mockGithubApiFactory.Verify(x => x.Create(It.IsAny<string>(), null, It.IsAny<string>()), Times.Never);
        }
    }
}
