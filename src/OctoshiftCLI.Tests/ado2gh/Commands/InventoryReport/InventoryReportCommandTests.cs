using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub;
using OctoshiftCLI.AdoToGithub.Commands.InventoryReport;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<AdoInspectorServiceFactory> _mockAdoInspectorServiceFactory = TestHelpers.CreateMock<AdoInspectorServiceFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<OrgsCsvGeneratorService> _mockOrgsCsvGeneratorService = TestHelpers.CreateMock<OrgsCsvGeneratorService>();
        private readonly Mock<TeamProjectsCsvGeneratorService> _mockTeamProjectsCsvGeneratorService = TestHelpers.CreateMock<TeamProjectsCsvGeneratorService>();
        private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGeneratorService = TestHelpers.CreateMock<ReposCsvGeneratorService>();
        private readonly Mock<PipelinesCsvGeneratorService> _mockPipelinesCsvGeneratorService = TestHelpers.CreateMock<PipelinesCsvGeneratorService>();

        private readonly ServiceProvider _serviceProvider;
        private readonly InventoryReportCommand _command = [];

        public InventoryReportCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object)
                .AddSingleton(_mockAdoInspectorServiceFactory.Object)
                .AddSingleton(_mockOrgsCsvGeneratorService.Object)
                .AddSingleton(_mockTeamProjectsCsvGeneratorService.Object)
                .AddSingleton(_mockReposCsvGeneratorService.Object)
                .AddSingleton(_mockPipelinesCsvGeneratorService.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("inventory-report", _command.Name);
            Assert.Equal(4, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", false);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "minimal", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Uses_The_Ado_Pat_When_Provided()
        {
            var adoPat = "ado-pat";

            var args = new InventoryReportCommandArgs
            {
                AdoOrg = "foo-org",
                AdoPat = adoPat,
            };

            _command.BuildHandler(args, _serviceProvider);

            _mockAdoApiFactory.Verify(m => m.Create(adoPat));
        }
    }
}
