using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class AdoApiTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<AdoClient> _mockAdoClient = TestHelpers.CreateMock<AdoClient>();

    private readonly AdoApi sut;

    private const string ADO_SERVICE_URL = "https://dev.azure.com";
    private const string USER_ID = "foo-user-id";
    private const string ADO_ORG = "foo org";
    private const string ADO_ORG_ID = "blah";
    private const string ADO_TEAM_PROJECT = "foo (tp)";
    private readonly string ADO_TEAM_PROJECT_ID = Guid.NewGuid().ToString();
    private const string ADO_REPO = "repo-1";
    private const string GITHUB_ORG = "foo-gh-org";

    public AdoApiTests()
    {
        sut = new AdoApi(client: _mockAdoClient.Object, adoServerUrl: ADO_SERVICE_URL, log: _mockOctoLogger.Object);
    }

    [Fact]
    public async Task GetUserId_Should_Return_UserId()
    {
        var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
        var userJson = new
        {
            coreAttributes = new
            {
                PublicAlias = new
                {
                    value = USER_ID
                }
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson.ToJson());

        var result = await sut.GetUserId();

        result.Should().Be(USER_ID);
    }

    [Fact]
    public async Task GetUserId_Invalid_Json_Should_Throw_InvalidDataException()
    {
        var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
        var userJson = new
        {
            invalid = new
            {
                PublicAlias = new
                {
                    value = USER_ID
                }
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson.ToJson());

        await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
    }

    [Fact]
    public async Task GetOrganizations_Should_Return_All_Orgs()
    {
        var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={USER_ID}?api-version=5.0-preview.1";
        var accountsJson = new object[]
        {
            new
            {
                accountId = "blah",
                AccountName = "foo"
            },
            new
            {
                AccountName = "foo2"
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(accountsJson.ToJson());

        var result = await sut.GetOrganizations(USER_ID);

        result.Count().Should().Be(2);
        result.Should().Contain(new[] { "foo", "foo2" });
    }

    [Fact]
    public async Task GetOrganizationId_Should_Return_OrgId()
    {
        var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={USER_ID}&api-version=5.0-preview.1";
        var accountsJson = new object[]
        {
            new
            {
                accountId = ADO_ORG_ID,
                accountName = ADO_ORG
            },
            new
            {
                accountName = "foo2",
                accountId = "asdf"
            }
        };

        var response = JArray.Parse(accountsJson.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

        var result = await sut.GetOrganizationId(USER_ID, ADO_ORG);

        result.Should().Be(ADO_ORG_ID);
    }

    [Fact]
    public async Task GetTeamProjects_Should_Return_All_Team_Projects()
    {
        var teamProject2 = "foo-tp2";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/projects?api-version=6.1-preview";
        var json = new object[]
        {
            new
            {
                somethingElse = false,
                name = ADO_TEAM_PROJECT
            },
            new
            {
                id = "sfasfasdf",
                name = teamProject2
            }
        };
        var response = JArray.Parse(json.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

        var result = await sut.GetTeamProjects(ADO_ORG);

        result.Count().Should().Be(2);
        result.Should().Contain(new[] { ADO_TEAM_PROJECT, teamProject2 });
    }

    [Fact]
    public async Task GetEnabledRepos_Should_Not_Return_Disabled_Repos()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories?api-version=6.1-preview.1";
        var repo1 = new AdoRepository { Id = "1", Name = ADO_REPO, Size = 123, IsDisabled = false };
        var repo2 = new AdoRepository { Id = "2", Name = "foo-repo2", Size = 5678, IsDisabled = false };
        var json = new object[]
        {
            new
            {
                isDisabled = true,
                name = "testing",
                size = 1234
            },
            new
            {
                id = repo1.Id,
                isDisabled = repo1.IsDisabled,
                name = repo1.Name,
                size = repo1.Size
            },
            new
            {
                id = repo2.Id,
                isDisabled = "FALSE",
                name = repo2.Name,
                size = repo2.Size
            }
        };
        var response = JArray.Parse(json.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

        var result = await sut.GetEnabledRepos(ADO_ORG, ADO_TEAM_PROJECT);

        result.Count().Should().Be(2);
        result.Should().BeEquivalentTo(new[] { repo1, repo2 });
    }

    [Fact]
    public async Task GetGithubAppId_Should_Skip_Team_Projects_With_No_Endpoints()
    {
        var teamProject2 = "foo-tp2";
        var teamProjects = new List<string>() { ADO_TEAM_PROJECT, teamProject2 };
        var appId = Guid.NewGuid().ToString();

        var json = new object[]
        {
            new
            {
                type = "GitHub",
                name = GITHUB_ORG,
                id = appId
            }
        };
        var response = JArray.Parse(json.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
        _mockAdoClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{teamProject2.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

        var result = await sut.GetGithubAppId(ADO_ORG, GITHUB_ORG, teamProjects);

        result.Should().Be(appId);
    }

    [Fact]
    public async Task GetGithubAppId_Should_Return_Null_When_No_Team_Projects_Have_Endpoint()
    {
        var teamProject2 = "foo-tp2";
        var teamProjects = new List<string>() { ADO_TEAM_PROJECT, teamProject2 };
        var appId = Guid.NewGuid().ToString();

        var json = new object[]
        {
            new
            {
                type = "GitHub",
                name = "wrongOrg",
                id = appId
            }
        };
        var response = JArray.Parse(json.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
        _mockAdoClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{teamProject2.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

        var result = await sut.GetGithubAppId(ADO_ORG, GITHUB_ORG, teamProjects);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGithubAppId_Should_Recognize_GitHubProximaPipelines_Service_Connection()
    {
        var teamProjects = new List<string>() { ADO_TEAM_PROJECT };
        var appId = Guid.NewGuid().ToString();

        var json = new object[]
        {
            new
            {
                type = "GitHubProximaPipelines",
                name = ADO_TEAM_PROJECT,
                id = appId
            }
        };
        var response = JArray.Parse(json.ToJson());

        _mockAdoClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

        var result = await sut.GetGithubAppId(ADO_ORG, GITHUB_ORG, teamProjects);

        result.Should().Be(appId);
    }

    [Fact]
    public async Task GetGithubHandle_Should_Return_Handle()
    {
        var githubToken = Guid.NewGuid().ToString();

        var handle = "FOO-LOGIN";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.github-user-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    accessToken = githubToken,
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.github-user-data-provider\": {{ \"login\": '{handle}' }} }} }}";

        _mockAdoClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

        var result = await sut.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, githubToken);

        result.Should().Be(handle);
    }

    [Fact]
    public async Task GetGithubHandle_Throws_On_Missing_Data_Provider()
    {
        var githubToken = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.github-user-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    accessToken = githubToken,
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        var json = $"{{ \"dataProviders\": {{ }} }}";

        _mockAdoClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

        var exception = await Assert.ThrowsAsync<OctoshiftCliException>(() => sut.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, githubToken));
        exception.Message.Should().Be("Missing data from 'ms.vss-work-web.github-user-data-provider'. Please ensure the Azure DevOps project has a configured GitHub connection.");
    }

    [Fact]
    public async Task GetBoardsGithubConnection_Should_Return_Connection_With_All_Repos()
    {
        var connectionId = "foo-id";
        var endpointId = "foo-endpoint-id";
        var connectionName = "foo-name";
        var repo2 = "repo-2";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.azure-boards-external-connection-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    includeInvalidConnections = false,
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.azure-boards-external-connection-data-provider\": {{ \"externalConnections\": [ {{ id: '{connectionId}', serviceEndpoint: {{ id: '{endpointId}' }}, name: '{connectionName}', externalGitRepos: [ {{ id: '{ADO_REPO}' }}, {{ id: '{repo2}' }} ] }}, {{ thisIsIgnored: true }} ]  }} }} }}";

        _mockAdoClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

        var result = await sut.GetBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT);

        result.connectionId.Should().Be(connectionId);
        result.endpointId.Should().Be(endpointId);
        result.connectionName.Should().Be(connectionName);
        result.repoIds.Count().Should().Be(2);
        result.repoIds.Should().Contain(new[] { ADO_REPO, repo2 });
    }

    [Fact]
    public async Task CreateBoardsGithubEndpoint_Should_Return_EndpointId()
    {
        var githubToken = Guid.NewGuid().ToString();
        var githubHandle = "foo-handle";
        var endpointName = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT_ID.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1";

        var payload = new
        {
            type = "githubboards",
            url = "http://github.com",
            authorization = new
            {
                scheme = "PersonalAccessToken",
                parameters = new
                {
                    accessToken = githubToken
                }
            },
            data = new
            {
                GitHubHandle = githubHandle
            },
            name = endpointName
        };

        var endpointId = "foo-id";
        var json = new
        {
            id = endpointId,
            name = "something"
        };

        _mockAdoClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json.ToJson());

        var result = await sut.CreateBoardsGithubEndpoint(ADO_ORG, ADO_TEAM_PROJECT_ID, githubToken, githubHandle, endpointName);

        result.Should().Be(endpointId);
    }

    [Fact]
    public async Task AddRepoToBoardsGithubConnection_Should_Send_Correct_Payload()
    {
        var connectionId = Guid.NewGuid().ToString();
        var connectionName = "FOO-CONNECTION";
        var endpointId = Guid.NewGuid().ToString();
        var repo2 = "repo-2";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.azure-boards-save-external-connection-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    externalConnection = new
                    {
                        serviceEndpointId = endpointId,
                        connectionName,
                        connectionId,
                        operation = 1,
                        externalRepositoryExternalIds = new[]
                        {
                            ADO_REPO,
                            repo2
                        },
                        providerKey = "github.com",
                        isGitHubApp = false
                    },
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        await sut.AddRepoToBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, connectionId, connectionName, endpointId, [ADO_REPO, repo2]);

        _mockAdoClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
    }

    [Fact]
    public async Task GetTeamProjectId_Should_Return_TeamProjectId()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/projects/{ADO_TEAM_PROJECT.EscapeDataString()}?api-version=5.0-preview.1";
        var response = new { id = ADO_TEAM_PROJECT_ID };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.GetTeamProjectId(ADO_ORG, ADO_TEAM_PROJECT);

        result.Should().Be(ADO_TEAM_PROJECT_ID);
    }

    [Fact]
    public async Task GetRepoId_Should_Return_RepoId()
    {
        var repoId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}?api-version=4.1";
        var response = new { id = repoId };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(repoId);
    }

    [Fact]
    public async Task GetRepoId_Should_Work_On_Disabled_Repo()
    {
        var repoId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}?api-version=4.1";
        var allReposEndpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories?api-version=4.1";
        var response = new[] {
            new { name = "blah", id = Guid.NewGuid().ToString() },
            new { name = ADO_REPO, id = repoId }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Throws(new HttpRequestException(message: null, inner: null, statusCode: HttpStatusCode.NotFound));
        _mockAdoClient.Setup(x => x.GetWithPagingAsync(allReposEndpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(repoId);
    }

    [Fact]
    public async Task GetRepoId_Should_Ignore_Duplicate_Repo_Names()
    {
        var repoId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}?api-version=4.1";
        var allReposEndpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories?api-version=4.1";
        var response = new[] {
            new { name = ADO_REPO, id = repoId },
            new { name = ADO_REPO, id = Guid.NewGuid().ToString() }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Throws(new HttpRequestException(message: null, inner: null, statusCode: HttpStatusCode.NotFound));
        _mockAdoClient.Setup(x => x.GetWithPagingAsync(allReposEndpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(repoId);
    }

    [Fact]
    public async Task GetPullRequestCount_Should_Return_Count()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pullrequests?searchCriteria.status=all&api-version=7.1-preview.1";
        var expectedCount = 12;

        _mockAdoClient.Setup(x => x.GetCountUsingSkip(endpoint)).ReturnsAsync(expectedCount);

        var result = await sut.GetPullRequestCount(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetLastPushDate_Should_Return_LastPushDate()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pushes?$top=1&api-version=7.1-preview.2";
        var expectedDate = new DateTime(year: 2022, month: 2, day: 14);

        var response = new
        {
            value = new[]
            {
                new { date = expectedDate.ToShortDateString() }
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(response.ToJson());

        var result = await sut.GetLastPushDate(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(expectedDate);
    }

    [Fact]
    public async Task GetLastPushDate_Should_Return_MinDate_When_No_Pushes()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pushes?$top=1&api-version=7.1-preview.2";

        var response = "{ count: 0, value: [] }";

        _mockAdoClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(response);

        var result = await sut.GetLastPushDate(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public async Task GetLastPushDate_Should_Be_Locale_Independent()
    {
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pushes?$top=1&api-version=7.1-preview.2";
        var expectedDate = new DateTime(year: 2016, month: 4, day: 22);

        var response = new
        {
            value = new[]
            {
                new { date = "2016-04-22T23:39:04.2658909Z" }
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint)).ReturnsAsync(response.ToJson());

        CultureInfo.CurrentCulture = new CultureInfo(name: "en-AT"); // Austrian culture has reversed datetime format

        var result = await sut.GetLastPushDate(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO);

        result.Should().Be(expectedDate);
    }

    [Fact]
    public async Task GetCommitCountSince_Should_Return_Commit_Count()
    {
        var fromDate = new DateTime(year: 2022, month: 2, day: 14);
        var fromDateIso = "02/14/2022";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/commits?searchCriteria.fromDate={fromDateIso}&api-version=7.1-preview.1";
        var expectedCount = 12;

        _mockAdoClient.Setup(x => x.GetCountUsingSkip(endpoint)).ReturnsAsync(expectedCount);

        var result = await sut.GetCommitCountSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, fromDate);

        result.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetCommitCountSince_Should_Be_Locale_Independent()
    {
        var fromDate = new DateTime(year: 2022, month: 2, day: 14);
        var fromDateIso = "02/14/2022";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/commits?searchCriteria.fromDate={fromDateIso}&api-version=7.1-preview.1";
        var expectedCount = 12;

        _mockAdoClient.Setup(x => x.GetCountUsingSkip(endpoint)).ReturnsAsync(expectedCount);

        CultureInfo.CurrentCulture = new CultureInfo(name: "en-AT"); // Austrian culture has reversed datetime format

        var result = await sut.GetCommitCountSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, fromDate);

        result.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetPushersSince_Should_Return_Pushers()
    {
        var fromDate = new DateTime(year: 2022, month: 2, day: 14);
        var fromDateIso = "02/14/2022";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pushes?searchCriteria.fromDate={fromDateIso}&api-version=7.1-preview.1";
        var pusher1DisplayName = "Dylan";
        var pusher1UniqueName = "dsmith";
        var pusher2DisplayName = "Tom";
        var pusher2UniqueName = "tcruise";

        var response = new[]
        {
            new
            {
                pushedBy = new { displayName = pusher1DisplayName, uniqueName = pusher1UniqueName }
            },
            new
            {
                pushedBy = new { displayName = pusher2DisplayName, uniqueName = pusher2UniqueName }
            }
        }.ToJson();

        var responseArray = JArray.Parse(response);

        _mockAdoClient.Setup(x => x.GetWithPagingTopSkipAsync(endpoint, It.IsAny<Func<JToken, string>>()))
            .ReturnsAsync((string url, Func<JToken, string> selector) => responseArray.Select(selector));

        var result = await sut.GetPushersSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, fromDate);

        result.First().Should().Be("Dylan (dsmith)");
        result.Last().Should().Be("Tom (tcruise)");
    }

    [Fact]
    public async Task GetPushersSince_Should_Be_Locale_Independent()
    {
        var fromDate = new DateTime(year: 2022, month: 2, day: 14);
        var fromDateIso = "02/14/2022";
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{ADO_REPO.EscapeDataString()}/pushes?searchCriteria.fromDate={fromDateIso}&api-version=7.1-preview.1";
        var pusher1DisplayName = "Dylan";
        var pusher1UniqueName = "dsmith";
        var pusher2DisplayName = "Tom";
        var pusher2UniqueName = "tcruise";

        var response = new[]
        {
            new
            {
                pushedBy = new { displayName = pusher1DisplayName, uniqueName = pusher1UniqueName }
            },
            new
            {
                pushedBy = new { displayName = pusher2DisplayName, uniqueName = pusher2UniqueName }
            }
        }.ToJson();

        var responseArray = JArray.Parse(response);

        _mockAdoClient.Setup(x => x.GetWithPagingTopSkipAsync(endpoint, It.IsAny<Func<JToken, string>>()))
            .ReturnsAsync((string url, Func<JToken, string> selector) => responseArray.Select(selector));

        CultureInfo.CurrentCulture = new CultureInfo(name: "en-AT"); // Austrian culture has reversed datetime format

        var result = await sut.GetPushersSince(ADO_ORG, ADO_TEAM_PROJECT, ADO_REPO, fromDate);

        result.First().Should().Be("Dylan (dsmith)");
        result.Last().Should().Be("Tom (tcruise)");
    }

    [Fact]
    public async Task GetPipelines_Should_Return_All_Pipelines()
    {
        var repoId = Guid.NewGuid().ToString();
        var pipeline1 = "foo-pipe-1";
        var pipeline2 = "foo-pipe-2";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?repositoryId={repoId}&repositoryType=TfsGit&queryOrder=lastModifiedDescending";
        var response = new object[]
        {
            new
            {
                id = "whatever",
                name = pipeline1,
                path = "\\some-folder"
            },
            new
            {
                name = pipeline2,
                path = "\\"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetPipelines(ADO_ORG, ADO_TEAM_PROJECT, repoId);

        result.Count().Should().Be(2);
        result.Should().Contain(new[] { $"\\some-folder\\{pipeline1}", $"\\{pipeline2}" });
    }

    [Fact]
    public async Task GetPipelineId_Should_Return_PipelineId()
    {
        var pipeline = "foo-pipe";
        var pipelineId = 36383;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
        var response = new object[]
        {
            new
            {
                id = 123,
                name = "wrong",
                path = "\\"
            },
            new
            {
                id = pipelineId,
                name = pipeline.ToUpper(),
                path = "\\"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, pipeline);

        result.Should().Be(pipelineId);
    }

    [Fact]
    public async Task GetPipelineId_With_Pipeline_Path_Should_Return_PipelineId()
    {
        var pipeline = "\\some-folder\\another\\foo-pipe";
        var pipelineId = 36383;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
        var response = new object[]
        {
            new
            {
                id = 123,
                name = "wrong",
                path = "\\"
            },
            new
            {
                id = pipelineId,
                name = "FOO-PIPE",
                path = "\\some-folder\\another"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, pipeline);

        result.Should().Be(pipelineId);
    }

    [Fact]
    public async Task GetPipelineId_With_Name_And_No_Path_Should_Succeed_When_Only_One_Match()
    {
        var pipeline = "foo-pipe";
        var pipelineId = 36383;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
        var response = new object[]
        {
            new
            {
                id = 123,
                name = "blah",
                path = "\\"
            },
            new
            {
                id = pipelineId,
                name = "FOO-PIPE",
                path = "\\some-folder"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, pipeline);

        result.Should().Be(pipelineId);
    }

    [Fact]
    public async Task GetPipelineId_With_Name_And_No_Path_Should_Fail_When_Multiple_Matches()
    {
        var pipeline = "foo-pipe";
        var pipelineId = 36383;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
        var response = new object[]
        {
            new
            {
                id = 123,
                name = "foo-pipe",
                path = "\\some-other-folder"
            },
            new
            {
                id = pipelineId,
                name = "FOO-PIPE",
                path = "\\some-folder"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        await FluentActions
            .Invoking(async () => await sut.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, pipeline))
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPipelineId_When_Duplicate_Pipeline_Name_And_Path_Should_Ignore_Second_Pipeline()
    {
        var pipeline = "\\some-folder\\foo-pipe";
        var pipelineId = 36383;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
        var response = new object[]
        {
            new
            {
                id = pipelineId,
                name = "FOO-PIPE",
                path = "\\some-folder"
            },
            new
            {
                id = 123,
                name = "FOO-PIPE",
                path = "\\some-folder"
            }
        };

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

        var result = await sut.GetPipelineId(ADO_ORG, ADO_TEAM_PROJECT, pipeline);

        result.Should().Be(pipelineId);
    }

    [Fact]
    public async Task ContainsServiceConnections_When_ServiceConnection_Not_Shared_Should_Return_False()
    {
        var serviceConnectionId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns("null");

        var result = await sut.ContainsServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, serviceConnectionId);

        result.Should().BeFalse();
    }


    [Fact]
    public async Task ShareServiceConnection_Should_Send_Correct_Payload()
    {
        var serviceConnectionId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

        var payload = new[]
        {
            new
            {
                name = $"{ADO_ORG}-{ADO_TEAM_PROJECT}",
                projectReference = new
                {
                    id = ADO_TEAM_PROJECT_ID,
                    name = ADO_TEAM_PROJECT
                }
            }
        };

        await sut.ShareServiceConnection(ADO_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT_ID, serviceConnectionId);

        _mockAdoClient.Verify(m => m.PatchAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())));
    }

    [Fact]
    public async Task GetPipeline_Should_Return_Pipeline()
    {
        var pipelineId = 826263;
        var branchName = "foo-branch";
        var defaultBranch = $"refs/heads/{branchName}";
        var clean = "True";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";
        var response = new
        {
            repository = new
            {
                defaultBranch,
                clean,
                checkoutSubmodules = default(object)
            },
            triggers = new[]
            {
                new
                {
                    triggerType = "continuousIntegration",
                    branchFilters = new[] { "+refs/heads/main" }
                }
            }
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var (DefaultBranch, Clean, CheckoutSubmodules, Triggers) = await sut.GetPipeline(ADO_ORG, ADO_TEAM_PROJECT, pipelineId);

        DefaultBranch.Should().Be(branchName);
        Clean.Should().Be("true");
        CheckoutSubmodules.Should().Be("null");
        Triggers.Should().NotBeNull();
    }

    [Fact]
    public async Task IsPipelineEnabled_Should_Return_True_For_Enabled_Pipeline()
    {
        var pipelineId = 826263;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";
        var response = new
        {
            id = pipelineId,
            queueStatus = "enabled"
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.IsPipelineEnabled(ADO_ORG, ADO_TEAM_PROJECT, pipelineId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPipelineEnabled_Should_Return_False_For_Disabled_Pipeline()
    {
        var pipelineId = 826263;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";
        var response = new
        {
            id = pipelineId,
            queueStatus = "disabled"
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.IsPipelineEnabled(ADO_ORG, ADO_TEAM_PROJECT, pipelineId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPipelineEnabled_Should_Return_False_For_Paused_Pipeline()
    {
        var pipelineId = 826263;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";
        var response = new
        {
            id = pipelineId,
            queueStatus = "paused"
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.IsPipelineEnabled(ADO_ORG, ADO_TEAM_PROJECT, pipelineId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPipelineEnabled_Should_Return_True_For_Missing_QueueStatus()
    {
        var pipelineId = 826263;

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";
        var response = new
        {
            id = pipelineId
        };

        _mockAdoClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

        var result = await sut.IsPipelineEnabled(ADO_ORG, ADO_TEAM_PROJECT, pipelineId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetBoardsGithubRepoId_Should_Return_RepoId()
    {
        var endpointId = Guid.NewGuid().ToString();
        var githubRepo = "foo-repo";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.github-user-repository-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    projectId = ADO_TEAM_PROJECT_ID,
                    repoWithOwnerName = $"{GITHUB_ORG}/{githubRepo}",
                    serviceEndpointId = endpointId,
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        var repoId = Guid.NewGuid().ToString();
        var json = $@"{{dataProviders: {{ ""ms.vss-work-web.github-user-repository-data-provider"": {{ additionalProperties: {{ nodeId: '{repoId}' }} }} }} }}";

        _mockAdoClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

        var result = await sut.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT_ID, endpointId, GITHUB_ORG, githubRepo);

        result.Should().Be(repoId);
    }

    [Fact]
    public async Task CreateBoardsGithubConnection_Should_Send_Correct_Payload()
    {
        var endpointId = Guid.NewGuid().ToString();
        var repoId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-work-web.azure-boards-save-external-connection-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    externalConnection = new
                    {
                        serviceEndpointId = endpointId,
                        operation = 0,
                        externalRepositoryExternalIds = new[]
                        {
                            repoId
                        },
                        providerKey = "github.com",
                        isGitHubApp = false
                    },
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            project = ADO_TEAM_PROJECT
                        }
                    }
                }
            }
        };

        await sut.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, endpointId, repoId);

        _mockAdoClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
    }

    [Fact]
    public async Task GetOrgOwner_Returns_Owner()
    {
        var ownerName = "Dave";
        var ownerEmail = "dave@gmail.com";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

        var payload = new
        {
            contributionIds = new[]
            {
                "ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"
            },
            dataProviderContext = new
            {
                properties = new
                {
                    sourcePage = new
                    {
                        routeValues = new
                        {
                            adminPivot = "organizationOverview"
                        }
                    }
                }
            }
        };

        var json = $@"{{ dataProviders: {{ ""ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"": {{ currentOwner: {{ name: '{ownerName}', email: '{ownerEmail}' }} }} }} }}";

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson()))).ReturnsAsync(json);

        var result = await sut.GetOrgOwner(ADO_ORG);

        result.Should().Be($"{ownerName} ({ownerEmail})");
    }

    [Fact]
    public async Task DisableRepo_Should_Send_Correct_Payload()
    {
        var repoId = Guid.NewGuid().ToString();

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/{ADO_TEAM_PROJECT.EscapeDataString()}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";

        await sut.DisableRepo(ADO_ORG, ADO_TEAM_PROJECT, repoId);

        var payload = new { isDisabled = true };

        _mockAdoClient.Verify(m => m.PatchAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
    }

    [Fact]
    public async Task GetIdentityDescriptor_Should_Return_IdentityDescriptor()
    {
        var groupName = "foo-group";
        var identityDescriptor = "foo-id";

        var endpoint = $"https://vssps.dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/identities?searchFilter=General&filterValue={groupName}&queryMembership=None&api-version=6.1-preview.1";
        var response = $@"[{{ properties: {{ LocalScopeId: {{ $value: ""wrong"" }} }}, descriptor: ""blah"" }}, {{ descriptor: ""{identityDescriptor}"", properties: {{ LocalScopeId: {{ $value: ""{ADO_TEAM_PROJECT_ID}"" }} }} }}]";

        _mockAdoClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

        var result = await sut.GetIdentityDescriptor(ADO_ORG, ADO_TEAM_PROJECT_ID, groupName);

        result.Should().Be(identityDescriptor);
    }

    [Fact]
    public async Task LockRepo_Should_Send_Correct_Payload()
    {
        var repoId = Guid.NewGuid().ToString();
        var identityDescriptor = "foo-id";
        var gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";

        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/accesscontrolentries/{gitReposNamespace}?api-version=6.1-preview.1";

        var payload = new
        {
            token = $"repoV2/{ADO_TEAM_PROJECT_ID}/{repoId}",
            merge = true,
            accessControlEntries = new[]
            {
                new
                {
                    descriptor = identityDescriptor,
                    allow = 0,
                    deny = 56828,
                    extendedInfo = new
                    {
                        effectiveAllow = 0,
                        effectiveDeny = 56828,
                        inheritedAllow = 0,
                        inheritedDeny = 56828
                    }
                }
            }
        };

        await sut.LockRepo(ADO_ORG, ADO_TEAM_PROJECT_ID, repoId, identityDescriptor);

        _mockAdoClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
    }

    [Fact]
    public async Task IsCallerOrgAdmin_Returns_True_When_Caller_Is_Org_Admin()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/permissions/3e65f728-f8bc-4ecd-8764-7e378b19bfa7/2?api-version=6.0";
        const string responseJson = "{\"count\":1,\"value\":[true]}";

        _mockAdoClient.Setup(m => m.GetAsync(endpoint)).ReturnsAsync(responseJson);

        // Act
        var result = await sut.IsCallerOrgAdmin(ADO_ORG);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCallerOrgAdmin_Returns_False_When_Caller_Is_Not_Org_Admin()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/permissions/3e65f728-f8bc-4ecd-8764-7e378b19bfa7/2?api-version=6.0";
        const string responseJson = "{\"count\":1,\"value\":[false]}";

        _mockAdoClient.Setup(m => m.GetAsync(endpoint)).ReturnsAsync(responseJson);

        // Act
        var result = await sut.IsCallerOrgAdmin(ADO_ORG);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCallerOrgAdmin_Returns_First_Value_From_Value_Array()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/permissions/3e65f728-f8bc-4ecd-8764-7e378b19bfa7/2?api-version=6.0";
        const string responseJson = "{\"count\":3,\"value\":[true, false, false]}";

        _mockAdoClient.Setup(m => m.GetAsync(endpoint)).ReturnsAsync(responseJson);

        // Act
        var result = await sut.IsCallerOrgAdmin(ADO_ORG);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsCallerOrgAdmin_Returns_False_When_Response_Payload_Has_Empty_Value_Array()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/permissions/3e65f728-f8bc-4ecd-8764-7e378b19bfa7/2?api-version=6.0";
        const string responseJson = "{\"count\":0,\"value\":[]}";

        _mockAdoClient.Setup(m => m.GetAsync(endpoint)).ReturnsAsync(responseJson);

        // Act
        var result = await sut.IsCallerOrgAdmin(ADO_ORG);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsCallerOrgAdmin_Returns_False_When_Response_Payload_Has_No_Value()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/permissions/3e65f728-f8bc-4ecd-8764-7e378b19bfa7/2?api-version=6.0";
        const string responseJson = "{}";

        _mockAdoClient.Setup(m => m.GetAsync(endpoint)).ReturnsAsync(responseJson);

        // Act
        var result = await sut.IsCallerOrgAdmin(ADO_ORG);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetGithubHandle_Throws_When_Error_Message_In_Response()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var responseJson = new
        {
            dataProviders = new Dictionary<string, object>
            {
                ["ms.vss-work-web.github-user-data-provider"] = new
                {
                    errorMessage = "An error has occurred when validating credentials. Please use correct scope for PAT token"
                }
            }
        }.ToJson();

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(responseJson);

        // Act & Assert
        var exception = await FluentActions
            .Invoking(async () => await sut.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, "token"))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>();

        exception.Which.Message.Should().Contain("Error validating GitHub token");
        exception.Which.Message.Should().Contain("An error has occurred when validating credentials. Please use correct scope for PAT token");
    }

    [Fact]
    public async Task GetBoardsGithubRepoId_Throws_When_Error_Message_In_Response()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var responseJson = new
        {
            dataProviders = new Dictionary<string, object>
            {
                ["ms.vss-work-web.github-user-repository-data-provider"] = new
                {
                    errorMessage = "Specified argument was out of the range of valid values.\\r\\nParameter name: name"
                }
            }
        }.ToJson();

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(responseJson);

        // Act & Assert
        var exception = await FluentActions
            .Invoking(async () => await sut.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT_ID, "endpoint", GITHUB_ORG, "repo"))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>();

        exception.Which.Message.Should().Contain("Error getting GitHub repository information");
        exception.Which.Message.Should().Contain("Specified argument was out of the range of valid values");
    }

    [Fact]
    public async Task CreateBoardsGithubConnection_Throws_When_Error_Message_In_Response()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var responseJson = new
        {
            dataProviders = new Dictionary<string, object>
            {
                ["ms.vss-work-web.azure-boards-save-external-connection-data-provider"] = new
                {
                    errorMessage = "Error creating connection"
                }
            }
        }.ToJson();

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(responseJson);

        // Act & Assert
        var exception = await FluentActions
            .Invoking(async () => await sut.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, "endpoint", "repo"))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>();

        exception.Which.Message.Should().Contain("Error creating boards GitHub connection");
        exception.Which.Message.Should().Contain("Error creating connection");
    }

    [Fact]
    public async Task AddRepoToBoardsGithubConnection_Throws_When_Error_Message_In_Response()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var responseJson = new
        {
            dataProviders = new Dictionary<string, object>
            {
                ["ms.vss-work-web.azure-boards-save-external-connection-data-provider"] = new
                {
                    errorMessage = "Error adding repository"
                }
            }
        }.ToJson();

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(responseJson);

        // Act & Assert
        var exception = await FluentActions
            .Invoking(async () => await sut.AddRepoToBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, "connection", "name", "endpoint", new[] { "repo" }))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>();

        exception.Which.Message.Should().Contain("Error adding repository to boards GitHub connection");
        exception.Which.Message.Should().Contain("Error adding repository");
    }

    [Fact]
    public async Task GetGithubHandle_Should_Throw_When_Response_Is_Malformed()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var malformedResponse = "{ invalid json";

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(malformedResponse);

        // Act & Assert - should throw JsonReaderException when parsing malformed JSON
        await FluentActions
            .Invoking(async () => await sut.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, "token"))
            .Should()
            .ThrowExactlyAsync<JsonReaderException>();
    }

    [Fact]
    public async Task GetGithubHandle_Should_Throw_When_DataProviders_Missing()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var responseJson = new
        {
            someOtherField = "value"
        }.ToJson();

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(responseJson);

        // Act & Assert - should throw with clear message when data provider missing
        var exception = await FluentActions
            .Invoking(async () => await sut.GetGithubHandle(ADO_ORG, ADO_TEAM_PROJECT, "token"))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>();

        exception.Which.Message.Should().Contain("Missing data from 'ms.vss-work-web.github-user-data-provider'");
    }

    [Fact]
    public async Task GetBoardsGithubRepoId_Should_Throw_When_Response_Is_Malformed()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var malformedResponse = "{ invalid json";

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(malformedResponse);

        // Act & Assert - should throw JsonReaderException when parsing malformed JSON
        await FluentActions
            .Invoking(async () => await sut.GetBoardsGithubRepoId(ADO_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT_ID, "endpoint", GITHUB_ORG, "repo"))
            .Should()
            .ThrowExactlyAsync<JsonReaderException>();
    }

    [Fact]
    public async Task CreateBoardsGithubConnection_Should_Throw_When_Response_Is_Malformed()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var malformedResponse = "{ invalid json";

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(malformedResponse);

        // Act & Assert - should throw JsonReaderException when parsing malformed JSON
        await FluentActions
            .Invoking(async () => await sut.CreateBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, "endpoint", "repo"))
            .Should()
            .ThrowExactlyAsync<JsonReaderException>();
    }

    [Fact]
    public async Task AddRepoToBoardsGithubConnection_Should_Throw_When_Response_Is_Malformed()
    {
        // Arrange
        var endpoint = $"https://dev.azure.com/{ADO_ORG.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
        var malformedResponse = "{ invalid json";

        _mockAdoClient.Setup(m => m.PostAsync(endpoint, It.IsAny<object>())).ReturnsAsync(malformedResponse);

        // Act & Assert - should throw JsonReaderException when parsing malformed JSON
        await FluentActions
            .Invoking(async () => await sut.AddRepoToBoardsGithubConnection(ADO_ORG, ADO_TEAM_PROJECT, "connection", "name", "endpoint", new[] { "repo" }))
            .Should()
            .ThrowExactlyAsync<JsonReaderException>();
    }
}
