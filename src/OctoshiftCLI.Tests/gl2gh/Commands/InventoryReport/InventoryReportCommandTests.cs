using FluentAssertions;
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
        private readonly Mock<GitlabApiFactory> _mockGitlabApiFactory = TestHelpers.CreateMock<GitlabApiFactory>();
        private readonly Mock<GitlabInspectorServiceFactory> _mockGitlabInspectorServiceFactory = TestHelpers.CreateMock<GitlabInspectorServiceFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<GroupsCsvGeneratorService> _mockGroupsCsvGeneratorService = TestHelpers.CreateMock<GroupsCsvGeneratorService>();
        private readonly Mock<ProjectsCsvGeneratorService> _mockProjectsCsvGeneratorService = TestHelpers.CreateMock<ProjectsCsvGeneratorService>();

        private readonly ServiceProvider _serviceProvider;
        private readonly InventoryReportCommand _command = [];

        public InventoryReportCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockGitlabApiFactory.Object)
                .AddSingleton(_mockGitlabInspectorServiceFactory.Object)
                .AddSingleton(_mockGroupsCsvGeneratorService.Object)
                .AddSingleton(_mockProjectsCsvGeneratorService.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            _command.Should().NotBeNull();
            _command.Name.Should().Be("inventory-report");
            _command.Options.Count.Should().Be(6);

            TestHelpers.VerifyCommandOption(_command.Options, "gitlab-server-url", true);
            TestHelpers.VerifyCommandOption(_command.Options, "gitlab-group", false);
            TestHelpers.VerifyCommandOption(_command.Options, "gitlab-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(_command.Options, "minimal", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void BuildHandler_Creates_The_Handler()
        {
            var args = new InventoryReportCommandArgs
            {
                GitlabServerUrl = "https://gitlab.contoso.com",
                GitlabPat = "gitlab-pat"
            };

            var handler = _command.BuildHandler(args, _serviceProvider);

            handler.Should().NotBeNull();
            _mockGitlabApiFactory.Verify(m => m.Create(args.GitlabServerUrl, args.GitlabPat, args.NoSslVerify));
            _mockGitlabInspectorServiceFactory.Verify(m => m.Create(It.IsAny<GitlabApi>()));
        }
    }
}
