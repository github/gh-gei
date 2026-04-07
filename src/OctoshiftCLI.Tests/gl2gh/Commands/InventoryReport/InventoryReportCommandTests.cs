using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.GitlabToGithub;
using OctoshiftCLI.GitlabToGithub.Commands.InventoryReport;
using OctoshiftCLI.GitlabToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GitlabToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandTests
    {
        private readonly Mock<GitlabApi> _mockGitlabApi = TestHelpers.CreateMock<GitlabApi>();
        private readonly Mock<GitlabInspectorServiceFactory> _mockGitlabInspectorServiceFactory = TestHelpers.CreateMock<GitlabInspectorServiceFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<ProjectsCsvGeneratorService> _mockProjectsCsvGeneratorService = TestHelpers.CreateMock<ProjectsCsvGeneratorService>();
        private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGeneratorService = TestHelpers.CreateMock<ReposCsvGeneratorService>();

        private readonly ServiceProvider _serviceProvider;
        private readonly InventoryReportCommand _command = [];

        public InventoryReportCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockGitlabApi.Object)
                .AddSingleton(_mockGitlabInspectorServiceFactory.Object)
                .AddSingleton(_mockProjectsCsvGeneratorService.Object)
                .AddSingleton(_mockReposCsvGeneratorService.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("inventory-report", _command.Name);
            Assert.Equal(7, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "bbs-server-url", true);
            TestHelpers.VerifyCommandOption(_command.Options, "bbs-project", false);
            TestHelpers.VerifyCommandOption(_command.Options, "bbs-username", false);
            TestHelpers.VerifyCommandOption(_command.Options, "bbs-password", false);
            TestHelpers.VerifyCommandOption(_command.Options, "minimal", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
            TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
        }
    }
}
