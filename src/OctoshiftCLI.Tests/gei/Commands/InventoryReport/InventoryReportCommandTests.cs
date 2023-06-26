using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport;
using OctoshiftCLI.GithubEnterpriseImporter.Factories;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommandTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
        private readonly Mock<ISourceGithubApiFactory> _mockGithubApiFactory = new();
        private readonly Mock<ReposCsvGeneratorServiceFactory> _mockReposCsvGeneratorServiceFactory = TestHelpers.CreateMock<ReposCsvGeneratorServiceFactory>();

        private readonly ServiceProvider _serviceProvider;
        private readonly InventoryReportCommand _command = new();

        public InventoryReportCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockFileSystemProvider.Object)
                .AddSingleton(_mockGithubApiFactory.Object)
                .AddSingleton(_mockReposCsvGeneratorServiceFactory.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            var command = new InventoryReportCommand();
            command.Should().NotBeNull();
            command.Name.Should().Be("inventory-report");
            command.Options.Count.Should().Be(6);

            TestHelpers.VerifyCommandOption(command.Options, "github-org", true);
            TestHelpers.VerifyCommandOption(command.Options, "github-pat", false);
            TestHelpers.VerifyCommandOption(command.Options, "minimal", false);
            TestHelpers.VerifyCommandOption(command.Options, "ghes-api-url", false);
            TestHelpers.VerifyCommandOption(command.Options, "no-ssl-verify", false);
            TestHelpers.VerifyCommandOption(command.Options, "verbose", false);
        }

        [Fact]
        public void Creates_NoSsl_GithubApi_When_NoSsl_Arg_Is_True()
        {
            var args = new InventoryReportCommandArgs
            {
                GithubOrg = "foo",
                GhesApiUrl = "https://github.contoso.com",
                NoSslVerify = true,
            };

            _ = _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.CreateClientNoSsl(args.GhesApiUrl, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Creates_GithubApi_With_SSL_When_NoSSL_Arg_Is_False()
        {
            var args = new InventoryReportCommandArgs
            {
                GithubOrg = "foo",
                GhesApiUrl = "https://github.contoso.com",
            };

            _ = _command.BuildHandler(args, _serviceProvider);

            _mockGithubApiFactory.Verify(m => m.Create(args.GhesApiUrl, It.IsAny<string>()), Times.Once);
        }
    }
}
