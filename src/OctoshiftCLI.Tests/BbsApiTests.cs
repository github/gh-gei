using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests;

public class BbsApiTests
{

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<BbsClient> _mockBbsClient = TestHelpers.CreateMock<BbsClient>();

    private readonly BbsApi sut;

    private const string BBS_SERVICE_URL = "http://localhost:7990";
    private const string PROJECT_KEY = "TEST";
    private const string SLUG = "test-repo";
    private const long EXPORT_ID = 12345;

    public BbsApiTests()
    {
        sut = new BbsApi(_mockBbsClient.Object, BBS_SERVICE_URL, _mockOctoLogger.Object);
    }

    [Fact]
    public async Task StartExport_Should_Return_ExportId()
    {
        var endpoint = $"{BBS_SERVICE_URL}/rest/api/1.0/migration/exports";
        var requestPayload = new
        {
            repositoriesRequest = new
            {
                includes = new[]
                {
                    new
                    {
                        projectKey = PROJECT_KEY,
                        slug = SLUG
                    }
                }
            }
        };

        var responsePayload = new
        {
            id = EXPORT_ID
        };

        _mockBbsClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == requestPayload.ToJson()))).ReturnsAsync(responsePayload.ToJson());

        var result = await sut.StartExport(PROJECT_KEY, SLUG);

        result.Should().Be(EXPORT_ID);
    }

    [Fact]
    public async Task StartExport_Defaults_To_Wildcard()
    {
        var endpoint = $"{BBS_SERVICE_URL}/rest/api/1.0/migration/exports";
        var requestPayload = new
        {
            repositoriesRequest = new
            {
                includes = new[]
                {
                    new
                    {
                        projectKey = "*",
                        slug = "*"
                    }
                }
            }
        };

        var responsePayload = new
        {
            id = EXPORT_ID
        };

        _mockBbsClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == requestPayload.ToJson()))).ReturnsAsync(responsePayload.ToJson());

        var result = await sut.StartExport();

        result.Should().Be(EXPORT_ID);
    }

    [Fact]
    public async Task GetExport_Returns_Export_Details()
    {
        var endpoint = $"{BBS_SERVICE_URL}/rest/api/1.0/migration/exports/{EXPORT_ID}";
        var state = "INITIALISING";
        var message = "Still working on it!";
        var percentage = 0;

        var responsePayload = new
        {
            id = EXPORT_ID,
            state,
            progress = new
            {
                message,
                percentage
            }
        };

        _mockBbsClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(responsePayload.ToJson());

        var (actualState, actualMessage, actualPercentage) = await sut.GetExport(EXPORT_ID);

        actualState.Should().Be(state);
        actualMessage.Should().Be(message);
        actualPercentage.Should().Be(percentage);
    }

    [Fact]
    public async Task GetServerVersion_Returns_Server_Version()
    {
        var endpoint = $"{BBS_SERVICE_URL}/rest/api/1.0/application-properties";
        var version = "8.3.0";

        var responsePayload = new
        {
            version,
            buildNumber = "8003000",
            buildDate = "1659066041797"
        };

        _mockBbsClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(responsePayload.ToJson());

        var result = await sut.GetServerVersion();

        result.Should().Be(version);
    }

    [Fact]
    public async Task GetProjects_Returns_Projects()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects";
        var projectFoo = (Id: 1, Key: "PF", Name: "Foo");
        var projectBar = (Id: 2, Key: "PB", Name: "Bar");
        var responsePayload = new
        {
            size = 2,
            limit = 25,
            isLastPage = true,
            values = new[]
            {
                new
                {
                    key = projectFoo.Key,
                    id = projectFoo.Id,
                    name = projectFoo.Name
                },
                new
                {
                    key = projectBar.Key,
                    id = projectBar.Id,
                    name = projectBar.Name
                }
            },
            start = 0
        };
        _mockBbsClient.Setup(m => m.GetAsync(url)).ReturnsAsync(responsePayload.ToJson());

        // Act
        var response = await sut.GetProjects();

        //Assert
        response.Should().BeEquivalentTo(new[] { projectFoo, projectBar });
    }

    [Fact]
    public async Task GetRepos_Returns_Repositories_For_Project()
    {
        // Arrange
        const string fooProjectKey = "FP";
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos";
        var fooRepo = (Id: 1, Slug: "foorepo", Name: "FooRepo");
        var barRepo = (Id: 2, Slug: "barrepo", Name: "BarRepo");
        var responsePayload = new
        {
            size = 2,
            limit = 25,
            isLastPage = true,
            values = new[]
            {
                new
                {
                    slug = fooRepo.Slug,
                    id = fooRepo.Id,
                    name = fooRepo.Name
                },
                new
                {
                    slug = barRepo.Slug,
                    id = barRepo.Id,
                    name = barRepo.Name
                }
            },
            start = 0
        };
        _mockBbsClient.Setup(m => m.GetAsync(url)).ReturnsAsync(responsePayload.ToJson());

        // Act
        var response = await sut.GetRepos(fooProjectKey);

        // Assert
        response.Should().BeEquivalentTo(new[] { fooRepo, barRepo });
    }
}
