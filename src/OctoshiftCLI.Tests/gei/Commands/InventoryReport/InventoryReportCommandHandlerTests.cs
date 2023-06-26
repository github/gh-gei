using System.Threading.Tasks;
using Moq;
using OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport;
using OctoshiftCLI.GithubEnterpriseImporter.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommandHandlerTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<FileSystemProvider> _mockFileSystemProvider = TestHelpers.CreateMock<FileSystemProvider>();
        private readonly Mock<ReposCsvGeneratorService> _mockReposCsvGeneratorService = TestHelpers.CreateMock<ReposCsvGeneratorService>();

        private readonly InventoryReportCommandHandler _handler;

        private const string ORG = "FOO-SOURCE-ORG";
        private const string GHES_API_URL = "https://github.contoso.com/api/v3";
        private const string GITHUB_PAT = "abc123";

        public InventoryReportCommandHandlerTests()
        {
            _handler = new InventoryReportCommandHandler(
                _mockOctoLogger.Object,
                _mockFileSystemProvider.Object,
                _mockReposCsvGeneratorService.Object);
        }

        [Fact]
        public async Task Calls_ReposCsvGenerator_And_Writes_CSV_File()
        {
            // Arrange
            var csvText = "foo,bar";

            _mockReposCsvGeneratorService.Setup(m => m.Generate(GHES_API_URL, ORG, true)).ReturnsAsync(csvText);

            // Act
            var args = new InventoryReportCommandArgs
            {
                GithubOrg = ORG,
                GhesApiUrl = GHES_API_URL,
                GithubPat = GITHUB_PAT,
                Minimal = true,
            };
            await _handler.Handle(args);

            // Assert
            _mockFileSystemProvider.Verify(m => m.WriteAllTextAsync("repos.csv", csvText));
        }
    }
}
