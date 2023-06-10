using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class BbsApiTests
{

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<BbsClient> _mockBbsClient = TestHelpers.CreateMock<BbsClient>();

    private readonly BbsApi _sut;

    private const string BBS_SERVICE_URL = "http://localhost:7990";
    private const string PROJECT_KEY = "TEST";
    private const string SLUG = "test-repo";
    private const long EXPORT_ID = 12345;

    public BbsApiTests()
    {
        _sut = new BbsApi(_mockBbsClient.Object, BBS_SERVICE_URL, _mockOctoLogger.Object);
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

        var result = await _sut.StartExport(PROJECT_KEY, SLUG);

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

        var (actualState, actualMessage, actualPercentage) = await _sut.GetExport(EXPORT_ID);

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

        var result = await _sut.GetServerVersion();

        result.Should().Be(version);
    }

    [Fact]
    public async Task GetProjects_Returns_Projects()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects";
        var projectFoo = (Id: 1, Key: "PF", Name: "Foo");
        var projectBar = (Id: 2, Key: "PB", Name: "Bar");
        var response = new[]
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
        }.ToAsyncJTokenEnumerable();
        _mockBbsClient.Setup(m => m.GetAllAsync(url)).Returns(response);

        // Act
        var result = await _sut.GetProjects();

        //Assert
        result.Should().BeEquivalentTo(new[] { projectFoo, projectBar });
    }

    [Fact]
    public async Task GetRepos_Returns_Repositories_For_Project()
    {
        // Arrange
        const string fooProjectKey = "FP";
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos";
        var fooRepo = (Id: 1, Slug: "foorepo", Name: "FooRepo", Archived: true);
        var barRepo = (Id: 2, Slug: "barrepo", Name: "BarRepo", Archived: false);
        var response = new[]
        {
            new
            {
                slug = fooRepo.Slug,
                id = fooRepo.Id,
                name = fooRepo.Name,
                archived = fooRepo.Archived
            },
            new
            {
                slug = barRepo.Slug,
                id = barRepo.Id,
                name = barRepo.Name,
                archived = barRepo.Archived
            }
        }.ToAsyncJTokenEnumerable();

        _mockBbsClient.Setup(m => m.GetAllAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetRepos(fooProjectKey);

        // Assert
        result.Should().BeEquivalentTo(new[] { fooRepo, barRepo });
    }

    [Fact]
    public async Task GetRepositoryPullRequests_Returns_Pull_Requests_For_Repository()
    {
        // Arrange
        const string fooProjectKey = "FP";
        const string fooRepo = "foorepo";
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos/{fooRepo}/pull-requests";
        var fooPullRequest = (Id: 1, Name: "FooPullRequest");
        var barPullRequest = (Id: 2, Name: "BarPullRequest");
        var response = new[]
        {
            new
            {
                id = fooPullRequest.Id,
                name = fooPullRequest.Name
            },
            new
            {
                id = barPullRequest.Id,
                name = barPullRequest.Name
            }
        }.ToAsyncJTokenEnumerable();

        _mockBbsClient.Setup(m => m.GetAllAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetRepositoryPullRequests(fooProjectKey, fooRepo);

        // Assert
        result.Should().BeEquivalentTo(new[] { fooPullRequest, barPullRequest });
    }

    [Fact]
    public async Task GetRepositoryLatestCommit_Returns_Latest_Commit_For_Repository()
    {
        // Arrange
        const string fooProjectKey = "FP";
        const string fooRepo = "foorepo";
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos/{fooRepo}/commits?limit=1";

        var fooCommit = new {
            size = 1,
            limit = 25,
            isLastPage = true,
            values = new[]
            {
                new
                {
                    id = "1",
                    displayId = "user1",
                    author = new
                    {
                        name = "user1 name",
                        emailAddress = "user1@example.com"
                    },
                    authorTimestamp = 1548719707064,
                    committer = new
                    {
                        name = "user1",
                        emailAddress = "user1@example.com"
                    },
                    committerTimestamp = 1548719707064,
                    message = "Commit message",
                    parents = new
                    {
                        id = "3",
                        displayId = "abc"
                    }
                },
            }
        };

        Task<string> response = Task.FromResult(fooCommit.ToJson());

        _mockBbsClient.Setup(m => m.GetAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetRepositoryLatestCommit(fooProjectKey, fooRepo);

        // Assert
        result.Should().BeEquivalentTo(JObject.FromObject(fooCommit));
    }
}
