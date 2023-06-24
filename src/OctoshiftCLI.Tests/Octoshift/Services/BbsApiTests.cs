using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

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
    public async Task GetProject_Returns_Project()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{PROJECT_KEY}";
        var projectFoo = (Id: 1, Key: "PF", Name: "Foo");
        var response = new
        {
            key = projectFoo.Key,
            id = projectFoo.Id,
            name = projectFoo.Name
        };
        var task = Task.FromResult(response.ToJson());

        _mockBbsClient.Setup(m => m.GetAsync(url)).Returns(task);

        // Act
        var result = await _sut.GetProject(PROJECT_KEY);

        //Assert
        result.Should().BeEquivalentTo(projectFoo);
    }

    [Fact]
    public async Task GetRepos_Returns_Repositories_For_Project()
    {
        // Arrange
        const string fooProjectKey = "FP";
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos";
        var fooRepo = (Id: 1, Slug: "foorepo", Name: "FooRepo");
        var barRepo = (Id: 2, Slug: "barrepo", Name: "BarRepo");
        var response = new[]
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
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{fooProjectKey}/repos/{fooRepo}/pull-requests?state=all";
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
    public async Task GetRepositoryLatestCommitDate_Returns_Latest_Commit_For_Repository()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{PROJECT_KEY}/repos/{SLUG}/commits?limit=1";

        var expectedDate = new DateTime(2022, 2, 14, 5, 20, 0, DateTimeKind.Utc);
        var commit = new
        {
            values = new[]
            {
                new { authorTimestamp = 1644816000000 }
            }
        };

        var response = Task.FromResult(commit.ToJson());

        _mockBbsClient.Setup(m => m.GetAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetRepositoryLatestCommitDate(PROJECT_KEY, SLUG);

        // Assert
        result.Should().Be(expectedDate);
    }

    [Fact]
    public async Task GetRepositoryLatestCommitDate_Should_Return_Null_On_Non_Success_Response()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{PROJECT_KEY}/repos/{SLUG}/commits?limit=1";

        _mockBbsClient.Setup(m => m.GetAsync(It.Is<string>(x => x.StartsWith(url)))).ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.NotFound));

        // Act
        var result = await _sut.GetRepositoryLatestCommitDate(PROJECT_KEY, SLUG);

        // Assert
        result.Should().Be(null);
    }

    [Fact]
    public async Task GetIsRepositoryArchived_Returns_Repository_Archived_Field()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/rest/api/1.0/projects/{PROJECT_KEY}/repos/{SLUG}?fields=archived";

        var repo = new
        {
            archived = false,
        };

        var response = Task.FromResult(repo.ToJson());

        _mockBbsClient.Setup(m => m.GetAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetIsRepositoryArchived(PROJECT_KEY, SLUG);

        // Assert
        result.Should().Be(repo.archived);
    }

    [Fact]
    public async Task GetRepositorySize_Returns_Sizes()
    {
        // Arrange
        const string url = $"{BBS_SERVICE_URL}/projects/{PROJECT_KEY}/repos/{SLUG}/sizes";

        var sizes = new
        {
            repository = 10000UL,
            attachments = 10000UL
        };

        var response = Task.FromResult(sizes.ToJson());

        _mockBbsClient.Setup(m => m.GetAsync(It.Is<string>(x => x.StartsWith(url)))).Returns(response);

        // Act
        var result = await _sut.GetRepositoryAndAttachmentsSize(PROJECT_KEY, SLUG, "bbs-username", "bbs-password");

        // Assert
        result.Should().Be((sizes.repository, sizes.attachments));
    }
}
