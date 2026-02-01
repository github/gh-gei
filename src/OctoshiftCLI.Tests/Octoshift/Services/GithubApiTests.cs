using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class GithubApiTests
{
    private const string API_URL = "https://api.github.com";
    private const string UPLOADS_URL = "https://uploads.github.com";
    private readonly RetryPolicy _retryPolicy = new(TestHelpers.CreateMock<OctoLogger>().Object) { _httpRetryInterval = 0, _retryInterval = 0 };
    private readonly Mock<OctoLogger> _logMock = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<GithubClient> _githubClientMock = TestHelpers.CreateMock<GithubClient>();
    private readonly Mock<ArchiveUploader> _archiveUploader;

    private readonly GithubApi _githubApi;

    private const string GITHUB_ORG = "ORG_LOGIN";
    private const string GITHUB_ENTERPRISE = "ENTERPRISE_NAME";
    private const string GITHUB_REPO = "REPOSITORY_NAME";
    private const string TARGET_ORG = "TARGET_ORG";
    private const string LOG_URL = "URL";

    private readonly JObject GQL_ERROR_RESPONSE = JObject.Parse(@"
        {
            ""data"": {
                ""node"":null
            },
            ""errors"":[
                {
                    ""type"":""SERVICE_UNAVAILABLE"",
                    ""path"":[""node""],
                    ""locations"":[{""line"":1,""column"":19}],
                    ""message"":""GitHub Enterprise Importer is currently unavailable. Please try again later.""
                }]
        }");

    public GithubApiTests()
    {
        _archiveUploader = new Mock<ArchiveUploader>(
            _githubClientMock.Object,
            UPLOADS_URL,
            _logMock.Object,
            _retryPolicy,
            TestHelpers.CreateMock<EnvironmentVariableProvider>().Object);
        _githubApi = new GithubApi(_githubClientMock.Object, API_URL, _retryPolicy, _archiveUploader.Object);
    }

    [Fact]
    public async Task AddAutoLink_Calls_The_Right_Endpoint_With_Payload()
    {
        // Arrange
        const string adoOrg = "ADO_ORG";
        const string adoTeamProject = "ADO_TEAM_PROJECT";

        var keyPrefix = "AB#";
        var urlTemplate = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/";

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/autolinks";

        var payload = new
        {
            key_prefix = keyPrefix,
            url_template = urlTemplate
        };

        // Act
        await _githubApi.AddAutoLink(GITHUB_ORG, GITHUB_REPO, keyPrefix, urlTemplate);

        // Assert
        _githubClientMock.Verify(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task GetAutoLinks_Calls_The_Right_Endpoint()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/autolinks";

        _githubClientMock.Setup(x => x.GetAllAsync(It.IsAny<string>(), null)).Returns(AsyncEnumerable.Empty<JToken>());

        // Act
        await _githubApi.GetAutoLinks(GITHUB_ORG, GITHUB_REPO);

        // Assert
        _githubClientMock.Verify(m => m.GetAllAsync(url, null));
    }

    [Fact]
    public async Task DeleteAutoLink_Calls_The_Right_Endpoint()
    {
        // Arrange
        const int autoLinkId = 1;

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/autolinks/{autoLinkId}";

        // Act
        await _githubApi.DeleteAutoLink(GITHUB_ORG, GITHUB_REPO, autoLinkId);

        // Assert
        _githubClientMock.Verify(m => m.DeleteAsync(url, null));
    }

    [Fact]
    public async Task CreateTeam_Returns_Created_Team_Id_And_Slug()
    {
        // Arrange
        const string teamName = "TEAM_NAME";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var payload = new { name = teamName, privacy = "closed" };

        const string teamId = "TEAM_ID";
        const string teamSlug = "TEAM_SLUG";
        var response = $"{{\"id\": \"{teamId}\", \"slug\": \"{teamSlug}\"}}";

        _githubClientMock
            .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.CreateTeam(GITHUB_ORG, teamName);

        // Assert
        result.Should().Be((teamId, teamSlug));
    }

    [Fact]
    public async Task CreateTeam_Retries_On_500_Error_And_Returns_Existing_Team()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string teamId = "TEAM_ID";
        const string teamSlug = "TEAM_SLUG";

        var createUrl = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var getTeamsUrl = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var payload = new { name = teamName, privacy = "closed" };

        // Setup for the first call to fail with 500
        _githubClientMock
            .SetupSequence(m => m.PostAsync(createUrl, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ThrowsAsync(new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));

        // Setup for GetTeams call during retry logic
        var teamsResponse = new[]
        {
            new { id = teamId, name = teamName, slug = teamSlug }
        }.ToAsyncJTokenEnumerable();

        _githubClientMock
            .Setup(m => m.GetAllAsync(getTeamsUrl, null))
            .Returns(teamsResponse);

        // Act
        var result = await _githubApi.CreateTeam(GITHUB_ORG, teamName);

        // Assert
        result.Should().Be((teamId, teamSlug));
        _githubClientMock.Verify(m => m.PostAsync(createUrl, It.IsAny<object>(), null), Times.Once);
        _githubClientMock.Verify(m => m.GetAllAsync(getTeamsUrl, null), Times.Once);
    }

    [Fact]
    public async Task CreateTeam_Retries_On_502_Error_And_Creates_Team_On_Retry()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string teamId = "TEAM_ID";
        const string teamSlug = "TEAM_SLUG";

        var createUrl = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var getTeamsUrl = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var payload = new { name = teamName, privacy = "closed" };

        var successResponse = $"{{\"id\": \"{teamId}\", \"slug\": \"{teamSlug}\"}}";

        // Setup for the first call to fail with 502, second call to succeed
        _githubClientMock
            .SetupSequence(m => m.PostAsync(createUrl, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ThrowsAsync(new HttpRequestException("Bad Gateway", null, HttpStatusCode.BadGateway))
            .ReturnsAsync(successResponse);

        // Setup for GetTeams call during retry logic (team doesn't exist yet)
        var emptyTeamsResponse = Array.Empty<JToken>().ToAsyncJTokenEnumerable();
        _githubClientMock
            .Setup(m => m.GetAllAsync(getTeamsUrl, null))
            .Returns(emptyTeamsResponse);

        // Act
        var result = await _githubApi.CreateTeam(GITHUB_ORG, teamName);

        // Assert
        result.Should().Be((teamId, teamSlug));
        _githubClientMock.Verify(m => m.PostAsync(createUrl, It.IsAny<object>(), null), Times.Exactly(2));
        _githubClientMock.Verify(m => m.GetAllAsync(getTeamsUrl, null), Times.Once);
    }

    [Fact]
    public async Task CreateTeam_Does_Not_Retry_On_400_Error()
    {
        // Arrange
        const string teamName = "TEAM_NAME";

        var createUrl = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        var payload = new { name = teamName, privacy = "closed" };

        // Setup for the call to fail with 400 (client error)
        _githubClientMock
            .Setup(m => m.PostAsync(createUrl, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ThrowsAsync(new HttpRequestException("Bad Request", null, HttpStatusCode.BadRequest));

        // Act & Assert
        await FluentAssertions.FluentActions
            .Invoking(async () => await _githubApi.CreateTeam(GITHUB_ORG, teamName))
            .Should()
            .ThrowAsync<HttpRequestException>()
            .Where(ex => ex.Message.Contains("Bad Request"));

        _githubClientMock.Verify(m => m.PostAsync(createUrl, It.IsAny<object>(), null), Times.Once);
    }

    [Fact]
    public async Task GetTeams_Returns_All_Teams()
    {
        // Arrange
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";

        var team1 = (Id: "1", Name: "TEAM_1", Slug: "SLUG_1");
        var team2 = (Id: "2", Name: "TEAM_2", Slug: "SLUG_2");
        var team3 = (Id: "3", Name: "TEAM_3", Slug: "SLUG_3");
        var team4 = (Id: "4", Name: "TEAM_4", Slug: "SLUG_4");

        var teamsResult = new[]
        {
            new { id = 1, name = team1.Name, slug = team1.Slug },
            new { id = 2, name = team2.Name, slug = team2.Slug },
            new { id = 3, name = team3.Name, slug = team3.Slug },
            new { id = 4, name = team4.Name, slug = team4.Slug }
        }.ToAsyncJTokenEnumerable();

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(teamsResult);

        // Act
        var result = (await _githubApi.GetTeams(GITHUB_ORG)).ToArray();

        // Assert
        result.Should().HaveCount(4);
        result.Should().Equal(team1, team2, team3, team4);
    }

    [Fact]
    public async Task GetTeamMembers_Returns_Team_Members()
    {
        // Arrange
        const string teamName = "TEAM_NAME";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/members?per_page=100";

        const string teamMember1 = "TEAM_MEMBER_1";
        const string teamMember2 = "TEAM_MEMBER_2";
        var responsePage1 = $@"
            [
                {{
                    ""login"": ""{teamMember1}"",
                    ""id"": 1
                }},
                {{
                    ""login"": ""{teamMember2}"", 
                    ""id"": 2
                }}
            ]";

        const string teamMember3 = "TEAM_MEMBER_3";
        const string teamMember4 = "TEAM_MEMBER_4";
        var responsePage2 = $@"
            [
                {{
                    ""login"": ""{teamMember3}"",
                    ""id"": 3
                }},
                {{
                    ""login"": ""{teamMember4}"", 
                    ""id"": 4
                }}
            ]";

        async IAsyncEnumerable<JToken> GetAllPages()
        {
            var jArrayPage1 = JArray.Parse(responsePage1);
            yield return jArrayPage1[0];
            yield return jArrayPage1[1];

            var jArrayPage2 = JArray.Parse(responsePage2);
            yield return jArrayPage2[0];
            yield return jArrayPage2[1];

            await Task.CompletedTask;
        }

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(GetAllPages);

        // Act
        var result = (await _githubApi.GetTeamMembers(GITHUB_ORG, teamName)).ToArray();

        // Assert
        result.Should().HaveCount(4);
        result.Should().Equal(teamMember1, teamMember2, teamMember3, teamMember4);
    }

    [Fact]
    public async Task GetTeamMembers_Retries_On_404()
    {
        // Arrange
        const string teamName = "TEAM_NAME";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/members?per_page=100";

        _githubClientMock
            .SetupSequence(m => m.GetAllAsync(url, null))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.NotFound))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.NotFound))
            .Returns(new[]
            {
                new { login = "Sally", id = 1 }
            }.ToAsyncJTokenEnumerable());

        // Act
        var result = (await _githubApi.GetTeamMembers(GITHUB_ORG, teamName)).ToArray();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRepos_Returns_Names_And_Visibility_Of_All_Repositories()
    {
        // Arrange
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/repos?per_page=100";

        const string repoName1 = "FOO";
        const string repoName2 = "BAR";
        var responsePage1 = $@"
            [
                {{
                    ""id"": 1,
                    ""name"": ""{repoName1}"",
                    ""visibility"": ""private"",
                }},
                {{
                    ""id"": 2,
                    ""name"": ""{repoName2}"",
                    ""visibility"": ""private"",
                }}
            ]";

        const string repoName3 = "BAZ";
        const string repoName4 = "QUX";
        var responsePage2 = $@"
            [
                {{
                    ""id"": 3,
                    ""name"": ""{repoName3}"",
                    ""visibility"": ""internal"",
                }},
                {{
                    ""id"": 4,
                    ""name"": ""{repoName4}"",
                    ""visibility"": ""public"",
                }}
            ]";

        async IAsyncEnumerable<JToken> GetAllPages()
        {
            var jArrayPage1 = JArray.Parse(responsePage1);
            yield return jArrayPage1[0];
            yield return jArrayPage1[1];

            var jArrayPage2 = JArray.Parse(responsePage2);
            yield return jArrayPage2[0];
            yield return jArrayPage2[1];

            await Task.CompletedTask;
        }

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(GetAllPages);

        // Act
        var result = (await _githubApi.GetRepos(GITHUB_ORG)).ToArray();

        // Assert
        result.Should().HaveCount(4);
        result.Should().Equal((repoName1, "private"), (repoName2, "private"), (repoName3, "internal"), (repoName4, "public"));
    }

    [Fact]
    public async Task DoesRepoExist_Returns_True_When_200()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        _githubClientMock.Setup(m => m.GetNonSuccessAsync(url, HttpStatusCode.NotFound)).ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.OK));

        // Act
        var result = await _githubApi.DoesRepoExist(GITHUB_ORG, GITHUB_REPO);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DoesRepoExist_Returns_False_When_404()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        _githubClientMock.Setup(m => m.GetNonSuccessAsync(url, HttpStatusCode.NotFound)).ReturnsAsync("Not Found");

        // Act
        var result = await _githubApi.DoesRepoExist(GITHUB_ORG, GITHUB_REPO);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DoesRepoExist_Returns_False_When_301()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        _githubClientMock.Setup(m => m.GetNonSuccessAsync(url, HttpStatusCode.MovedPermanently)).ReturnsAsync("Moved Permanently");

        // Act
        var result = await _githubApi.DoesRepoExist(GITHUB_ORG, GITHUB_REPO);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DoesRepoExist_Throws_On_Unexpected_Response()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        _githubClientMock.Setup(m => m.GetNonSuccessAsync(url, HttpStatusCode.NotFound)).ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.Unauthorized));

        // Act
        await FluentActions
        .Invoking(async () => await _githubApi.DoesRepoExist(GITHUB_ORG, GITHUB_REPO))
        .Should()
        .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DoesOrgExist_Returns_True_When_200()
    {
        // Arrange
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}";

        _githubClientMock.Setup(m => m.GetAsync(url, null)).ReturnsAsync("OK");

        // Act
        var result = await _githubApi.DoesOrgExist(GITHUB_ORG);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DoesOrgExist_Returns_False_When_404()
    {
        // Arrange
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}";

        _githubClientMock.Setup(m => m.GetAsync(url, null)).ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.NotFound));

        // Act
        var result = await _githubApi.DoesOrgExist(GITHUB_ORG);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DoesOrgExist_Throws_On_Unexpected_Response()
    {
        // Arrange
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}";

        _githubClientMock.Setup(m => m.GetAsync(url, null)).ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.Unauthorized));

        // Act
        await FluentActions
        .Invoking(async () => await _githubApi.DoesOrgExist(GITHUB_ORG))
        .Should()
        .ThrowExactlyAsync<HttpRequestException>();
    }

    [Fact]
    public async Task RemoveTeamMember_Calls_The_Right_Endpoint()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string member = "MEMBER";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/memberships/{member}";

        _githubClientMock.Setup(m => m.DeleteAsync(url, null));

        // Act
        await _githubApi.RemoveTeamMember(GITHUB_ORG, teamName, member);

        // Assert
        _githubClientMock.Verify(m => m.DeleteAsync(url, null));
    }

    [Fact]
    public async Task RemoveTeamMember_Retries_On_Exception()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string member = "MEMBER";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/memberships/{member}";

        _githubClientMock.SetupSequence(m => m.DeleteAsync(url, null))
                         .ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.BadGateway))
                         .ReturnsAsync(string.Empty);

        // Act
        await _githubApi.RemoveTeamMember(GITHUB_ORG, teamName, member);

        // Assert
        _githubClientMock.Verify(m => m.DeleteAsync(url, null), Times.Exactly(2));
    }

    [Fact]
    public async Task GetOrgMembershipForUser_Returns_User_Role()
    {
        // Arrange
        var member = "USER";
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/memberships/{member}";
        var role = "admin";
        var response = $@"
            {{
                ""role"": ""{role}"" 
            }}";

        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetOrgMembershipForUser(GITHUB_ORG, member);

        // Assert
        result.Should().Match(role);
    }

    [Fact]
    public async Task GetOrgMembershipForUser_Returns_Empty_On_HTTP_Exception()
    {
        // Arrange
        var member = "USER";
        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/memberships/{member}";

        _githubClientMock
            .SetupSequence(m => m.GetAsync(url, null))
            .ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.NotFound))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _githubApi.GetOrgMembershipForUser(GITHUB_ORG, member);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddTeamSync_Calls_The_Right_Endpoint_With_Payload()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string groupId = "GROUP_ID";
        const string groupName = "GROUP_NAME";
        const string groupDesc = "GROUP_DESC";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/team-sync/group-mappings";
        var payload = new
        {
            groups = new[] { new { group_id = groupId, group_name = groupName, group_description = groupDesc } }
        };

        // Act
        await _githubApi.AddTeamSync(GITHUB_ORG, teamName, groupId, groupName, groupDesc);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task AddTeamToRepo_Calls_The_Right_Endpoint_With_Payload()
    {
        // Arrange
        const string teamName = "TEAM_NAME";
        const string role = "ROLE";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamName}/repos/{GITHUB_ORG}/{GITHUB_REPO}";
        var payload = new { permission = role };

        // Act
        await _githubApi.AddTeamToRepo(GITHUB_ORG, GITHUB_REPO, teamName, role);

        // Assert
        _githubClientMock.Verify(m => m.PutAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task GetOrganizationId_Returns_The_Org_Id()
    {
        // Arrange
        const string orgId = "ORG_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($login: String!) {{organization(login: $login) {{ login, id, name }} }}\",\"variables\":{{\"login\":\"{GITHUB_ORG}\"}}}}";
        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""organization"": 
                            {{
                                ""login"": ""{GITHUB_ORG}"",
                                ""id"": ""{orgId}"",
                                ""name"": ""github"" 
                            }} 
                    }} 
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetOrganizationId(GITHUB_ORG);

        // Assert
        result.Should().Be(orgId);
    }

    [Fact]
    public async Task GetOrganizationId_Retries_On_GQL_Error()
    {
        // Arrange
        const string orgId = "ORG_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($login: String!) {{organization(login: $login) {{ login, id, name }} }}\",\"variables\":{{\"login\":\"{GITHUB_ORG}\"}}}}";

        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""organization"": 
                            {{
                                ""login"": ""{GITHUB_ORG}"",
                                ""id"": ""{orgId}"",
                                ""name"": ""github"" 
                            }} 
                    }} 
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetOrganizationId(GITHUB_ORG);

        // Assert
        result.Should().Be(orgId);
    }

    [Fact]
    public async Task GetOrganizationDatabaseId_Returns_The_Database_Id()
    {
        // Arrange
        const string databaseId = "DATABASE_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($login: String!) {{organization(login: $login) {{ login, databaseId, name }} }}\",\"variables\":{{\"login\":\"{GITHUB_ORG}\"}}}}";
        var response = JObject.Parse($@"
        {{
            ""data"": {{
                ""organization"": {{
                    ""login"": ""{GITHUB_ORG}"",
                    ""name"": ""github"",
                    ""databaseId"": ""{databaseId}""
                }}
            }}
        }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetOrganizationDatabaseId(GITHUB_ORG);

        // Assert
        result.Should().Be(databaseId);
    }

    [Fact]
    public async Task GetOrganizationDatabaseId_Retries_On_GQL_Error()
    {
        // Arrange
        const string databaseId = "DATABASE_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($login: String!) {{organization(login: $login) {{ login, databaseId, name }} }}\",\"variables\":{{\"login\":\"{GITHUB_ORG}\"}}}}";

        var response = JObject.Parse($@"
           {{
                ""data"": 
                    {{
                        ""organization"": 
                            {{
                                ""login"": ""{GITHUB_ORG}"",
                                ""databaseId"": ""{databaseId}"",
                                ""name"": ""github"" 
                            }} 
                    }} 
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetOrganizationDatabaseId(GITHUB_ORG);

        // Assert
        result.Should().Be(databaseId);
    }


    [Fact]
    public async Task GetEnterpriseId_Returns_The_Enterprise_Id()
    {
        // Arrange
        const string enterpriseId = "ENTERPRISE_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($slug: String!) {{enterprise (slug: $slug) {{ slug, id }} }}\",\"variables\":{{\"slug\":\"{GITHUB_ENTERPRISE}\"}}}}";
        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""enterprise"": 
                            {{
                                ""slug"": ""{GITHUB_ENTERPRISE}"",
                                ""id"": ""{enterpriseId}""
                            }} 
                    }} 
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetEnterpriseId(GITHUB_ENTERPRISE);

        // Assert
        result.Should().Be(enterpriseId);
    }

    [Fact]
    public async Task GetEnterpriseId_Retries_On_GQL_Error()
    {
        // Arrange
        const string enterpriseId = "ENTERPRISE_ID";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($slug: String!) {{enterprise (slug: $slug) {{ slug, id }} }}\",\"variables\":{{\"slug\":\"{GITHUB_ENTERPRISE}\"}}}}";

        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""enterprise"": 
                            {{
                                ""slug"": ""{GITHUB_ENTERPRISE}"",
                                ""id"": ""{enterpriseId}""
                            }} 
                    }} 
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetEnterpriseId(GITHUB_ENTERPRISE);

        // Assert
        result.Should().Be(enterpriseId);
    }

    [Fact]
    public async Task CreateAdoMigrationSource_Returns_New_Migration_Source_Id()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";
        const string orgId = "ORG_ID";
        var payload =
            "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!) " +
            "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } } }\"" +
            $",\"variables\":{{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\"}},\"operationName\":\"createMigrationSource\"}}";
        const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""Azure Devops Source"",
                            ""url"": ""https://dev.azure.com"",
                            ""type"": ""AZURE_DEVOPS""
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var expectedMigrationSourceId = await _githubApi.CreateAdoMigrationSource(orgId, null);

        // Assert
        expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
    }

    [Fact]
    public async Task CreateAdoMigrationSource_Uses_Ado_Server_Url()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";
        const string orgId = "ORG_ID";
        const string adoServerUrl = "https://ado.contoso.com";
        var payload =
            "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!) " +
            "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } } }\"" +
            $",\"variables\":{{\"name\":\"Azure DevOps Source\",\"url\":\"{adoServerUrl}\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\"}},\"operationName\":\"createMigrationSource\"}}";
        const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""Azure Devops Source"",
                            ""url"": ""{adoServerUrl}"",
                            ""type"": ""AZURE_DEVOPS""
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var expectedMigrationSourceId = await _githubApi.CreateAdoMigrationSource(orgId, adoServerUrl);

        // Assert
        expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
    }

    [Fact]
    public async Task CreateBbsMigrationSource_Returns_New_Migration_Source_Id()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";
        const string orgId = "ORG_ID";
        var payload =
            "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!) " +
            "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } } }\"" +
            $",\"variables\":{{\"name\":\"Bitbucket Server Source\",\"url\":\"https://not-used\",\"ownerId\":\"{orgId}\",\"type\":\"BITBUCKET_SERVER\"}},\"operationName\":\"createMigrationSource\"}}";
        const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""Bitbucket Server Source"",
                            ""url"": ""https://not-used"",
                            ""type"": ""BITBUCKET_SERVER""
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var expectedMigrationSourceId = await _githubApi.CreateBbsMigrationSource(orgId);

        // Assert
        expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
    }

    [Fact]
    public async Task CreateGhecMigrationSource_Returns_New_Migration_Source_Id()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";
        const string orgId = "ORG_ID";
        var payload =
            "{\"query\":\"mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!) " +
            "{ createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } } }\"" +
            $",\"variables\":{{\"name\":\"GHEC Source\",\"url\":\"https://github.com\",\"ownerId\":\"{orgId}\",\"type\":\"GITHUB_ARCHIVE\"}},\"operationName\":\"createMigrationSource\"}}";
        const string actualMigrationSourceId = "MS_kgC4NjFhOTVjOTc4ZTRhZjEwMDA5NjNhOTdm";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""createMigrationSource"": {{
                        ""migrationSource"": {{
                            ""id"": ""{actualMigrationSourceId}"",
                            ""name"": ""GHEC Source"",
                            ""url"": ""https://github.com"",
                            ""type"": ""GITHUB_ARCHIVE""
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var expectedMigrationSourceId = await _githubApi.CreateGhecMigrationSource(orgId);

        // Assert
        expectedMigrationSourceId.Should().Be(actualMigrationSourceId);
    }

    [Fact]
    public async Task StartMigration_Returns_New_Repository_Migration_Id()
    {
        // Arrange
        const string migrationSourceId = "MIGRATION_SOURCE_ID";
        const string adoRepoUrl = "ADO_REPO_URL";
        const string orgId = "ORG_ID";
        const string url = "https://api.github.com/graphql";
        const string gitArchiveUrl = "GIT_ARCHIVE_URL";
        const string metadataArchiveUrl = "METADATA_ARCHIVE_URL";
        const string sourceToken = "SOURCE_TOKEN";
        const string targetToken = "TARGET_TOKEN";

        const string query = @"
                mutation startRepositoryMigration(
                    $sourceId: ID!,
                    $ownerId: ID!,
                    $sourceRepositoryUrl: URI!,
                    $repositoryName: String!,
                    $continueOnError: Boolean!,
                    $gitArchiveUrl: String,
                    $metadataArchiveUrl: String,
                    $accessToken: String!,
                    $githubPat: String,
                    $skipReleases: Boolean,
                    $targetRepoVisibility: String,
                    $lockSource: Boolean)";
        const string gql = @"
                startRepositoryMigration(
                    input: { 
                        sourceId: $sourceId,
                        ownerId: $ownerId,
                        sourceRepositoryUrl: $sourceRepositoryUrl,
                        repositoryName: $repositoryName,
                        continueOnError: $continueOnError,
                        gitArchiveUrl: $gitArchiveUrl,
                        metadataArchiveUrl: $metadataArchiveUrl,
                        accessToken: $accessToken,
                        githubPat: $githubPat,
                        skipReleases: $skipReleases,
                        targetRepoVisibility: $targetRepoVisibility,
                        lockSource: $lockSource
                    }
                ) {
                    repositoryMigration {
                        id,
                        databaseId,
                        migrationSource {
                            id,
                            name,
                            type
                        },
                        sourceUrl,
                        state,
                        failureReason
                    }
                  }";
        var payload = new
        {
            query = $"{query} {{ {gql} }}",
            variables = new
            {
                sourceId = migrationSourceId,
                ownerId = orgId,
                sourceRepositoryUrl = adoRepoUrl,
                repositoryName = GITHUB_REPO,
                continueOnError = true,
                gitArchiveUrl,
                metadataArchiveUrl,
                accessToken = sourceToken,
                githubPat = targetToken,
                skipReleases = false,
                targetRepoVisibility = (string)null,
                lockSource = false
            },
            operationName = "startRepositoryMigration"
        };
        const string actualRepositoryMigrationId = "RM_kgC4NjFhNmE2NGU2ZWE1YTQwMDA5ODliZjhi";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""startRepositoryMigration"": {{
                        ""repositoryMigration"": {{
                            ""id"": ""{actualRepositoryMigrationId}"",
                            ""databaseId"": ""3ba25b34-b23d-43fb-a819-f44414be8dc0"",
                            ""migrationSource"": {{
                                ""id"": ""MS_kgC4NjFhNmE2NDViNWZmOTEwMDA5MTZiMGQw"",
                                ""name"": ""Azure Devops Source"",
                                ""type"": ""AZURE_DEVOPS""
                            }},
                        ""sourceUrl"": ""https://dev.azure.com/github-inside-msft/Team-Demos/_git/Tiny"",
                        ""state"": ""QUEUED"",
                        ""failureReason"": """"
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var expectedRepositoryMigrationId = await _githubApi.StartMigration(migrationSourceId, adoRepoUrl, orgId, GITHUB_REPO, sourceToken, targetToken, gitArchiveUrl, metadataArchiveUrl);

        // Assert
        expectedRepositoryMigrationId.Should().Be(actualRepositoryMigrationId);
    }

    [Fact]
    public async Task StartBbsMigration_Returns_New_Repository_Migration_Id()
    {
        // Arrange
        const string migrationSourceId = "MIGRATION_SOURCE_ID";
        const string sourceRepoUrl = "https://our-bbs-server.com/projects/BBS-PROJECT/repos/bbs-repo/browse";
        const string orgId = "ORG_ID";
        const string url = "https://api.github.com/graphql";
        const string gitArchiveUrl = "GIT_ARCHIVE_URL";
        const string targetToken = "TARGET_TOKEN";

        const string unusedSourceToken = "not-used";
        const string unusedMetadataArchiveUrl = "https://not-used";

        const string query = @"
                mutation startRepositoryMigration(
                    $sourceId: ID!,
                    $ownerId: ID!,
                    $sourceRepositoryUrl: URI!,
                    $repositoryName: String!,
                    $continueOnError: Boolean!,
                    $gitArchiveUrl: String,
                    $metadataArchiveUrl: String,
                    $accessToken: String!,
                    $githubPat: String,
                    $skipReleases: Boolean,
                    $targetRepoVisibility: String,
                    $lockSource: Boolean)";
        const string gql = @"
                startRepositoryMigration(
                    input: { 
                        sourceId: $sourceId,
                        ownerId: $ownerId,
                        sourceRepositoryUrl: $sourceRepositoryUrl,
                        repositoryName: $repositoryName,
                        continueOnError: $continueOnError,
                        gitArchiveUrl: $gitArchiveUrl,
                        metadataArchiveUrl: $metadataArchiveUrl,
                        accessToken: $accessToken,
                        githubPat: $githubPat,
                        skipReleases: $skipReleases,
                        targetRepoVisibility: $targetRepoVisibility,
                        lockSource: $lockSource
                    }
                ) {
                    repositoryMigration {
                        id,
                        databaseId,
                        migrationSource {
                            id,
                            name,
                            type
                        },
                        sourceUrl,
                        state,
                        failureReason
                    }
                  }";
        var payload = new
        {
            query = $"{query} {{ {gql} }}",
            variables = new
            {
                sourceId = migrationSourceId,
                ownerId = orgId,
                sourceRepositoryUrl = sourceRepoUrl,
                repositoryName = GITHUB_REPO,
                continueOnError = true,
                gitArchiveUrl,
                metadataArchiveUrl = unusedMetadataArchiveUrl,
                accessToken = unusedSourceToken,
                githubPat = targetToken,
                skipReleases = false,
                targetRepoVisibility = (string)null,
                lockSource = false
            },
            operationName = "startRepositoryMigration"
        };
        const string actualRepositoryMigrationId = "RM_kgC4NjFhNmE2NGU2ZWE1YTQwMDA5ODliZjhi";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""startRepositoryMigration"": {{
                        ""repositoryMigration"": {{
                            ""id"": ""{actualRepositoryMigrationId}"",
                            ""databaseId"": ""3ba25b34-b23d-43fb-a819-f44414be8dc0"",
                            ""migrationSource"": {{
                                ""id"": ""MS_kgC4NjFhNmE2NDViNWZmOTEwMDA5MTZiMGQw"",
                                ""name"": ""Azure Devops Source"",
                                ""type"": ""AZURE_DEVOPS""
                            }},
                        ""sourceUrl"": ""https://dev.azure.com/github-inside-msft/Team-Demos/_git/Tiny"",
                        ""state"": ""QUEUED"",
                        ""failureReason"": """"
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var expectedRepositoryMigrationId = await _githubApi.StartBbsMigration(migrationSourceId, sourceRepoUrl, orgId, GITHUB_REPO, targetToken, gitArchiveUrl);

        // Assert
        expectedRepositoryMigrationId.Should().Be(actualRepositoryMigrationId);
    }

    [Fact]
    public async Task StartMigration_Does_Not_Throw_When_Errors_Is_Empty()
    {
        // Arrange
        var response = JObject.Parse(@"
            {
                ""data"": { 
                    ""startRepositoryMigration"": {
                        ""repositoryMigration"": {
                            ""id"": ""RM_kgC4NjFhNmE2NGU2ZWE1YTQwMDA5ODliZjhi""
                         }
                     }
                 },
                ""errors"": []
            }");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(It.IsAny<string>(), It.IsAny<object>(), null))
            .ReturnsAsync(response);

        // Act, Assert
        await _githubApi.Invoking(api => api.StartMigration(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Should()
            .NotThrowAsync<OctoshiftCliException>();
    }

    [Fact]
    public async Task GetMigration_Returns_The_Migration_State_Repository_Name_And_Warnings_Count()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";

        var query = "query($id: ID!)";
        var gql = @"
                node(id: $id) {
                    ... on Migration {
                        id,
                        sourceUrl,
                        migrationLogUrl,
                        migrationSource {
                            name
                        },
                        state,
                        warningsCount,
                        failureReason,
                        repositoryName
                    }
                }";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

        const string actualMigrationState = "SUCCEEDED";
        const int actualWarningsCount = 3;

        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""{actualMigrationState}"",
                        ""warningsCount"": {actualWarningsCount},
                        ""failureReason"": """",
                        ""repositoryName"": ""{GITHUB_REPO}"",
                        ""migrationLogUrl"": ""{LOG_URL}""
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, expectedRepositoryName, expectedWarningsCount, expectedFailureReason, migrationLogUrl) = await _githubApi.GetMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
        expectedRepositoryName.Should().Be(GITHUB_REPO);
        expectedWarningsCount.Should().Be(actualWarningsCount);
        expectedFailureReason.Should().BeEmpty();
        migrationLogUrl.Should().Be(LOG_URL);
    }

    [Fact]
    public async Task GetMigration_Retries_On_Http_502()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";

        var query = "query($id: ID!)";
        var gql = @"
                node(id: $id) {
                    ... on Migration {
                        id,
                        sourceUrl,
                        migrationLogUrl,
                        migrationSource {
                            name
                        },
                        state,
                        warningsCount,
                        failureReason,
                        repositoryName
                    }
                }";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

        const string actualMigrationState = "SUCCEEDED";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""{actualMigrationState}"",
                        ""warningsCount"": 0,
                        ""failureReason"": """",
                        ""migrationLogUrl"": ""{LOG_URL}""
                    }}
                }}
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.BadGateway))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.BadGateway))
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, _, _, _, _) = await _githubApi.GetMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
    }

    [Fact]
    public async Task GetMigration_Retries_On_GQL_Error()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";

        var query = "query($id: ID!)";
        var gql = @"
                node(id: $id) {
                    ... on Migration {
                        id,
                        sourceUrl,
                        migrationLogUrl,
                        migrationSource {
                            name
                        },
                        state,
                        warningsCount,
                        failureReason,
                        repositoryName
                    }
                }";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

        const string actualMigrationState = "SUCCEEDED";

        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""{actualMigrationState}"",
                        ""warningsCount"": 0,
                        ""failureReason"": """",
                        ""migrationLogUrl"": ""{LOG_URL}""
                    }}
                }}
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, _, _, _, _) = await _githubApi.GetMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
    }

    [Fact]
    public async Task GetMigration_Returns_The_Migration_Failure_Reason()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";

        var query = "query($id: ID!)";
        var gql = @"
                node(id: $id) {
                    ... on Migration {
                        id,
                        sourceUrl,
                        migrationLogUrl,
                        migrationSource {
                            name
                        },
                        state,
                        warningsCount,
                        failureReason,
                        repositoryName
                    }
                }";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

        const string actualFailureReason = "FAILURE_REASON";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""id"": ""RM_kgC4NjFhNmE2ZWY1NmE4MjAwMDA4NjA5NTZi"",
                        ""sourceUrl"": ""https://github.com/import-testing/archive-export-testing"",
                        ""migrationSource"": {{
                            ""name"": ""GHEC Archive Source""
                        }},
                        ""state"": ""FAILED"",
                        ""warningsCount"": 0,
                        ""failureReason"": ""{actualFailureReason}"",
                        ""repositoryName"": ""{GITHUB_REPO}"",
                        ""migrationLogUrl"": ""{LOG_URL}""
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var (_, _, _, expectedFailureReason, _) = await _githubApi.GetMigration(migrationId);

        // Assert
        expectedFailureReason.Should().Be(actualFailureReason);
    }

    [Fact]
    public async Task GetOrganizationMigration_Returns_The_Migration_State_And_Org_URL_And_Name_And_Progress_Information()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";
        const string sourceOrgUrl = "https://github.com/import-testing";

        var payload =
            "{\"query\":\"query($id: ID!) { node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } } }\"" +
            $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
        const string actualMigrationState = "SUCCEEDED";
        const int actualRemainingRepositoriesCount = 0;
        const int actualTotalRepositoriesCount = 9000;
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""sourceOrgUrl"": ""{sourceOrgUrl}"",
                        ""state"": ""{actualMigrationState}"",
                        ""failureReason"": """",
                        ""targetOrgName"": ""{TARGET_ORG}"",
                        ""remainingRepositoriesCount"": {actualRemainingRepositoriesCount},
                        ""totalRepositoriesCount"": {actualTotalRepositoriesCount}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, expectedSourceOrgUrl, expectedTargetOrgName, expectedFailureReason, expectedRemainingRepositoriesCount, expectedTotalRepositoriesCount) = await _githubApi.GetOrganizationMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
        expectedSourceOrgUrl.Should().Be(sourceOrgUrl);
        expectedTargetOrgName.Should().Be(TARGET_ORG);
        expectedFailureReason.Should().BeEmpty();
        expectedRemainingRepositoriesCount.Should().Be(actualRemainingRepositoriesCount);
        expectedTotalRepositoriesCount.Should().Be(actualTotalRepositoriesCount);
    }

    [Fact]
    public async Task GetOrganizationMigration_Retries_On_502()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";
        const string sourceOrgUrl = "https://github.com/import-testing";

        var payload =
            "{\"query\":\"query($id: ID!) { node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } } }\"" +
            $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
        const string actualMigrationState = "SUCCEEDED";
        const int actualRemainingRepositoriesCount = 0;
        const int actualTotalRepositoriesCount = 9000;
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""sourceOrgUrl"": ""{sourceOrgUrl}"",
                        ""state"": ""{actualMigrationState}"",
                        ""failureReason"": """",
                        ""targetOrgName"": ""{TARGET_ORG}"",
                        ""remainingRepositoriesCount"": {actualRemainingRepositoriesCount},
                        ""totalRepositoriesCount"": {actualTotalRepositoriesCount}
                    }}
                }}
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.BadGateway))
            .Throws(new HttpRequestException(null, null, statusCode: HttpStatusCode.BadGateway))
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, _, _, _, _, _) = await _githubApi.GetOrganizationMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
    }

    [Fact]
    public async Task GetOrganizationMigration_Retries_On_GQL_Error()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";
        const string sourceOrgUrl = "https://github.com/import-testing";

        var payload =
            "{\"query\":\"query($id: ID!) { node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } } }\"" +
            $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
        const string actualMigrationState = "SUCCEEDED";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""sourceOrgUrl"": ""{sourceOrgUrl}"",
                        ""state"": ""{actualMigrationState}"",
                        ""failureReason"": """",
                        ""targetOrgName"": ""{TARGET_ORG}""
                    }}
                }}
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var (expectedMigrationState, _, _, _, _, _) = await _githubApi.GetOrganizationMigration(migrationId);

        // Assert
        expectedMigrationState.Should().Be(actualMigrationState);
    }

    [Fact]
    public async Task GetOrganizationMigration_Returns_The_Migration_Failure_Reason()
    {
        // Arrange
        const string migrationId = "MIGRATION_ID";
        const string url = "https://api.github.com/graphql";
        const string sourceOrgUrl = "https://github.com/import-testing";

        var payload =
            "{\"query\":\"query($id: ID!) { node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } } }\"" +
            $",\"variables\":{{\"id\":\"{migrationId}\"}}}}";
        const string actualFailureReason = "FAILURE_REASON";
        const int actualRemainingRepositoriesCount = 9000;
        const int actualTotalRepositoriesCount = 9000;
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""node"": {{
                        ""sourceOrgUrl"": ""{sourceOrgUrl}"",
                        ""state"": ""FAILED"",
                        ""failureReason"": ""{actualFailureReason}"",
                        ""targetOrgName"": ""{TARGET_ORG}"",
                        ""remainingRepositoriesCount"": {actualRemainingRepositoriesCount},
                        ""totalRepositoriesCount"": {actualTotalRepositoriesCount}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var (_, _, _, expectedFailureReason, _, _) = await _githubApi.GetOrganizationMigration(migrationId);

        // Assert
        expectedFailureReason.Should().Be(actualFailureReason);
    }

    [Fact]
    public async Task GetMigrationLogUrl_Returns_The_Migration_Log_URL_And_Migration_ID()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";

        var query = "query ($org: String!, $repo: String!)";
        var gql = @"
                organization(login: $org) {
                    repositoryMigrations(last: 1, repositoryName: $repo) {
                        nodes {
                            id
                            migrationLogUrl
                        }
                    }
                }
            ";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { org = GITHUB_ORG, repo = GITHUB_REPO } };

        const string migrationLogUrl = "MIGRATION_LOG_URL";
        const string migrationId = "MIGRATION_ID";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""organization"": {{
                        ""repositoryMigrations"": {{
                            ""nodes"": [
                                {{
                                    ""id"": ""{migrationId}"",
                                    ""migrationLogUrl"": ""{migrationLogUrl}""
                                }}
                            ]
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var expectedMigrationLog = await _githubApi.GetMigrationLogUrl(GITHUB_ORG, GITHUB_REPO);

        // Assert
        expectedMigrationLog.Should().Be((migrationLogUrl, migrationId));
    }

    [Fact]
    public async Task GetMigrationLogUrl_Retries_On_GQL_Error()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";

        var query = "query ($org: String!, $repo: String!)";
        var gql = @"
                organization(login: $org) {
                    repositoryMigrations(last: 1, repositoryName: $repo) {
                        nodes {
                            id
                            migrationLogUrl
                        }
                    }
                }
            ";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { org = GITHUB_ORG, repo = GITHUB_REPO } };

        const string migrationLogUrl = "MIGRATION_LOG_URL";
        const string migrationId = "MIGRATION_ID";
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""organization"": {{
                        ""repositoryMigrations"": {{
                            ""nodes"": [
                                {{
                                    ""id"": ""{migrationId}"",
                                    ""migrationLogUrl"": ""{migrationLogUrl}""
                                }}
                            ]
                        }}
                    }}
                }}
            }}");

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(GQL_ERROR_RESPONSE)
            .ReturnsAsync(response);

        // Act
        var expectedMigrationLog = await _githubApi.GetMigrationLogUrl(GITHUB_ORG, GITHUB_REPO);

        // Assert
        expectedMigrationLog.Should().Be((migrationLogUrl, migrationId));
    }

    [Fact]
    public async Task GetMigrationLogUrl_Returns_Null_When_No_Migration()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";

        var query = "query ($org: String!, $repo: String!)";
        var gql = @"
                organization(login: $org) {
                    repositoryMigrations(last: 1, repositoryName: $repo) {
                        nodes {
                            id
                            migrationLogUrl
                        }
                    }
                }
            ";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { org = GITHUB_ORG, repo = GITHUB_REPO } };
        var response = JObject.Parse(@"
            {
                ""data"": {
                    ""organization"": {
                        ""repositoryMigrations"": {
                            ""nodes"": [
                            ]
                        }
                    }
                }
            }");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response);

        var result = await _githubApi.GetMigrationLogUrl(GITHUB_ORG, GITHUB_REPO);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIdpGroupId_Returns_The_Idp_Group_Id()
    {
        // Arrange
        const string groupName = "GROUP_NAME";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/external-groups";
        const int expectedGroupId = 123;

        var group1 = new
        {
            group_id = expectedGroupId,
            group_name = groupName,
            updated_at = DateTime.Parse("2021-01-24T11:31:04-06:00")
        };
        var group2 = new
        {
            group_id = "456",
            group_name = "Octocat admins",
            updated_at = DateTime.Parse("2021-03-24T11:31:04-06:00")
        };

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, It.IsAny<Func<JToken, JArray>>(), null))
            .Returns(new[]
            {
                JToken.FromObject(group1),
                JToken.FromObject(group2)
            }.ToAsyncEnumerable());

        // Act
        var actualGroupId = await _githubApi.GetIdpGroupId(GITHUB_ORG, groupName);

        // Assert
        actualGroupId.Should().Be(expectedGroupId);
    }

    [Fact]
    public async Task GetTeamSlug_Returns_The_Team_Slug()
    {
        // Arrange
        const string teamName = "TEAM_NAME";

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams";
        const string expectedTeamSlug = "justice-league";

        var response = new[]
        {
            new
            {
                id = 1,
                node_id = "MDQ6VGVhbTE=",
                url = "https://api.github.com/teams/1",
                html_url = "https://github.com/orgs/github/teams/justice-league",
                name = teamName,
                slug = expectedTeamSlug,
                description = "A great team.",
                privacy = "closed",
                permission = "admin",
                members_url = "https://api.github.com/teams/1/members/member",
                repositories_url = "https://api.github.com/teams/1/repos",
            }
        }.ToAsyncJTokenEnumerable();

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(response);

        // Act
        var actualTeamSlug = await _githubApi.GetTeamSlug(GITHUB_ORG, teamName);

        // Assert
        actualTeamSlug.Should().Be(expectedTeamSlug);
    }

    [Fact]
    public async Task AddEmuGroupToTeam_Calls_The_Right_Endpoint_With_Payload()
    {
        // Arrange
        const string teamSlug = "TEAM_SLUG";
        const int groupId = 1;

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamSlug}/external-groups";
        var payload = new { group_id = groupId };

        // Act
        await _githubApi.AddEmuGroupToTeam(GITHUB_ORG, teamSlug, groupId);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task AddEmuGroupToTeam_Retries_On_400()
    {
        // Arrange
        const string teamSlug = "TEAM_SLUG";
        const int groupId = 1;

        var url = $"https://api.github.com/orgs/{GITHUB_ORG}/teams/{teamSlug}/external-groups";
        var payload = new { group_id = groupId };

        _githubClientMock.SetupSequence(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ThrowsAsync(new HttpRequestException(null, null, HttpStatusCode.BadRequest))
            .ReturnsAsync("");

        // Act
        await _githubApi.AddEmuGroupToTeam(GITHUB_ORG, teamSlug, groupId);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null), Times.Exactly(2));
    }

    [Fact]
    public async Task GrantMigratorRole_Returns_True_On_Success()
    {
        // Arrange
        const string actor = "ACTOR";
        const string actorType = "ACTOR_TYPE";
        const string url = "https://api.github.com/graphql";

        var payload =
            "{\"query\":\"mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
            "{ grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
            $",\"variables\":{{\"organizationId\":\"{GITHUB_ORG}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
            "\"operationName\":\"grantMigratorRole\"}";
        const bool expectedSuccessState = true;
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""grantMigratorRole"": {{
                        ""success"": {expectedSuccessState.ToString().ToLower()}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var actualSuccessState = await _githubApi.GrantMigratorRole(GITHUB_ORG, actor, actorType);

        // Assert
        actualSuccessState.Should().BeTrue();
    }

    [Fact]
    public async Task GrantMigratorRole_Returns_False_On_HttpRequestException()
    {
        // Arrange
        const string actor = "ACTOR";
        const string actorType = "ACTOR_TYPE";
        const string url = "https://api.github.com/graphql";

        var payload =
            "{\"query\":\"mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
            "{ grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
            $",\"variables\":{{\"organizationId\":\"{GITHUB_ORG}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
            "\"operationName\":\"grantMigratorRole\"}";

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .Throws<HttpRequestException>();

        // Act
        var actualSuccessState = await _githubApi.GrantMigratorRole(GITHUB_ORG, actor, actorType);

        // Assert
        actualSuccessState.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeMigratorRole_Returns_True_On_Success()
    {
        // Arrange
        const string actor = "ACTOR";
        const string actorType = "ACTOR_TYPE";
        const string url = "https://api.github.com/graphql";

        var payload =
            "{\"query\":\"mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
            "{ revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
            $",\"variables\":{{\"organizationId\":\"{GITHUB_ORG}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
            "\"operationName\":\"revokeMigratorRole\"}";
        const bool expectedSuccessState = true;
        var response = JObject.Parse($@"
            {{
                ""data"": {{
                    ""revokeMigratorRole"": {{
                        ""success"": {expectedSuccessState.ToString().ToLower()}
                    }}
                }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var actualSuccessState = await _githubApi.RevokeMigratorRole(GITHUB_ORG, actor, actorType);

        // Assert
        actualSuccessState.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeMigratorRole_Returns_False_On_HttpRequestException()
    {
        // Arrange
        const string actor = "ACTOR";
        const string actorType = "ACTOR_TYPE";
        const string url = "https://api.github.com/graphql";

        var payload =
            "{\"query\":\"mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! ) " +
            "{ revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success } }\"" +
            $",\"variables\":{{\"organizationId\":\"{GITHUB_ORG}\",\"actor\":\"{actor}\",\"actor_type\":\"{actorType}\"}}," +
            "\"operationName\":\"revokeMigratorRole\"}";

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .Throws<HttpRequestException>();

        // Act
        var actualSuccessState = await _githubApi.RevokeMigratorRole(GITHUB_ORG, actor, actorType);

        // Assert
        actualSuccessState.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRepo_Calls_The_Right_Endpoint()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        // Act
        await _githubApi.DeleteRepo(GITHUB_ORG, GITHUB_REPO);

        // Assert
        _githubClientMock.Verify(m => m.DeleteAsync(url, null));
    }

    [Fact]
    public async Task GetUserId_Returns_The_User_Id()
    {
        // Arrange
        const string login = "mona";
        const string userId = "NDQ5VXNlcjc4NDc5MzU=";

        var url = $"https://api.github.com/graphql";
        var payload =
            $"{{\"query\":\"query($login: String!) {{user(login: $login) {{ id, name }} }}\",\"variables\":{{\"login\":\"{login}\"}}}}";

        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""user"": 
                            {{
                                ""id"": ""{userId}"",
                                ""name"": ""{login}"" 
                            }} 
                    }} 
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => x.ToJson() == payload), null))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetUserId(login);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public async Task CreateAttributionInvitation_Returns_Error()
    {
        // Arrange
        const string orgId = "dummyorgid";
        const string mannequinId = "NDQ5VXNlcjc4NDc5MzU=";
        const string targetUserId = "ND5TVXNlcjc4NDc5MzU=";

        var url = $"https://api.github.com/graphql";

        var payload = @"{""query"":""mutation($orgId: ID!,$sourceId: ID!,$targetId: ID!) { createAttributionInvitation(
		            input: { ownerId: $orgId, sourceId: $sourceId, targetId: $targetId }
	            ) {
		            source {
			            ... on Mannequin {
				            id
				            login
			            }
		            }

		            target {
			            ... on User {
				            id
				            login
			            }
		            }
                }
            }""" + $",\"variables\":{{\"orgId\":\"{orgId}\", \"sourceId\":\"{mannequinId}\", \"targetId\":\"{targetUserId}\"}}}}";

        var response = $@"{{
                ""data"": {{
                                ""createAttributionInvitation"": null
                    }},
                ""errors"": [{{
                                ""type"": ""UNPROCESSABLE"",
                    ""path"": [""createAttributionInvitation""],
                    ""locations"": [{{
                                        ""line"": 2,
                        ""column"": 14
                    }}],
                    ""message"": ""Target must be a member of the octocat organization""
                }}]
            }}";

        var expecteCreateAttributionInvitationResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = null
            },
            Errors =
            [
                new ErrorData
                {
                    Type = "UNPROCESSABLE",
                    Message = "Target must be a member of the octocat organization",
                    Path = ["createAttributionInvitation"],
                    Locations =
                    [
                        new Location() { Line = 2, Column = 14 }
                    ]
                }
            ]
        };

        _githubClientMock
            .Setup(m => m.PostAsync(url,
            It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)), null))
                .ReturnsAsync(response);

        // Act
        var result = await _githubApi.CreateAttributionInvitation(orgId, mannequinId, targetUserId);

        // Assert
        result.Should().BeEquivalentTo(expecteCreateAttributionInvitationResponse);
    }

    [Fact]
    public async Task GetMannequins_Returns_NoMannequins()
    {
        // Arrange
        const string orgId = "ORG_ID";

        var url = $"https://api.github.com/graphql";

        var payload =
@"{""query"":""query($id: ID!, $first: Int, $after: String) { 
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            nodes {
                                login
                                id
                                claimant {
                                    login
                                    id
                                }
                            }
                        }
                    }
                }
            }""" +
$",\"variables\":{{\"id\":\"{orgId}\"}}}}";

        _githubClientMock
            .Setup(m => m.PostGraphQLWithPaginationAsync(
                url,
                It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)),
                It.IsAny<Func<JObject, JArray>>(),
                It.IsAny<Func<JObject, JObject>>(),
                It.IsAny<int>(),
                null,
                null))
                .Returns(Array.Empty<JToken>().ToAsyncEnumerable());

        // Act
        var result = await _githubApi.GetMannequins(orgId);

        // Assert
        result.Count().Should().Be(0);
    }

    [Fact]
    public async Task GetMannequins_Returns_Mannequins()
    {
        // Arrange
        const string orgId = "ORG_ID";
        const string login1 = "mona";
        const string login2 = "monalisa";

        var url = $"https://api.github.com/graphql";

        var payload =
@"{""query"":""query($id: ID!, $first: Int, $after: String) { 
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            nodes {
                                login
                                id
                                claimant {
                                    login
                                    id
                                }
                            }
                        }
                    }
                }
            }""" +
$",\"variables\":{{\"id\":\"{orgId}\"}}}}";

        var mannequin1 = new
        {
            login = "mona",
            id = "DUMMYID1",
            claimant = new { }
        };
        var mannequin2 = new
        {
            login = "monalisa",
            id = "DUMMYID2",
            claimant = new
            {
                login = "monareclaimed",
                id = "TARGETDUMMYID"
            }
        };

        _githubClientMock
            .Setup(m => m.PostGraphQLWithPaginationAsync(
                url,
                It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)),
                It.IsAny<Func<JObject, JArray>>(),
                It.IsAny<Func<JObject, JObject>>(),
                It.IsAny<int>(),
                null,
                null))
                .Returns(new[]
                    {
                        JToken.FromObject(mannequin1),
                        JToken.FromObject(mannequin2),
                    }.ToAsyncEnumerable());

        // Act
        var result = await _githubApi.GetMannequins(orgId);

        // Assert
        result.Count().Should().Be(2);

        var mannequinsResult = result.ToArray();
        mannequinsResult[0].Id.Should().Be("DUMMYID1");
        mannequinsResult[0].Login.Should().Be(login1);
        mannequinsResult[0].MappedUser.Should().BeNull();

        mannequinsResult[1].Id.Should().Be("DUMMYID2");
        mannequinsResult[1].Login.Should().Be(login2);
        mannequinsResult[1].MappedUser.Should().NotBeNull();
        mannequinsResult[1].MappedUser.Login.Should().Be("monareclaimed");
        mannequinsResult[1].MappedUser.Id.Should().Be("TARGETDUMMYID");
    }

    [Fact]
    public async Task GetMannequins_Retries_On_Error()
    {
        // Arrange
        const string orgId = "ORG_ID";
        const string login1 = "mona";
        const string login2 = "monalisa";

        var url = $"https://api.github.com/graphql";

        var payload =
@"{""query"":""query($id: ID!, $first: Int, $after: String) { 
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            nodes {
                                login
                                id
                                claimant {
                                    login
                                    id
                                }
                            }
                        }
                    }
                }
            }""" +
$",\"variables\":{{\"id\":\"{orgId}\"}}}}";

        var mannequin1 = new
        {
            login = "mona",
            id = "DUMMYID1",
            claimant = new { }
        };
        var mannequin2 = new
        {
            login = "monalisa",
            id = "DUMMYID2",
            claimant = new
            {
                login = "monareclaimed",
                id = "TARGETDUMMYID"
            }
        };

        _githubClientMock
            .SetupSequence(m => m.PostGraphQLWithPaginationAsync(
                url,
                It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)),
                It.IsAny<Func<JObject, JArray>>(),
                It.IsAny<Func<JObject, JObject>>(),
                It.IsAny<int>(),
                null,
                null))
                .Throws(new InvalidOperationException())
                .Throws(new InvalidOperationException())
                .Returns(new[]
                    {
                        JToken.FromObject(mannequin1),
                        JToken.FromObject(mannequin2),
                    }.ToAsyncEnumerable());

        // Act
        var result = await _githubApi.GetMannequins(orgId);

        // Assert
        result.Count().Should().Be(2);

        var mannequinsResult = result.ToArray();
        mannequinsResult[0].Id.Should().Be("DUMMYID1");
        mannequinsResult[0].Login.Should().Be(login1);
        mannequinsResult[0].MappedUser.Should().BeNull();

        mannequinsResult[1].Id.Should().Be("DUMMYID2");
        mannequinsResult[1].Login.Should().Be(login2);
        mannequinsResult[1].MappedUser.Should().NotBeNull();
        mannequinsResult[1].MappedUser.Login.Should().Be("monareclaimed");
        mannequinsResult[1].MappedUser.Id.Should().Be("TARGETDUMMYID");
    }

    [Fact]
    public async Task GetMannequinsByLogin_Returns_NoMannequins()
    {
        // Arrange
        const string orgId = "ORG_ID";
        const string login = "monalisa";

        var url = $"https://api.github.com/graphql";

        var payload =
@"{""query"":""query($id: ID!, $first: Int, $after: String, $login: String) { 
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after, login: $login) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            nodes {
                                login
                                id
                                claimant {
                                    login
                                    id
                                }
                            }
                        }
                    }
                }
            }""" +
$",\"variables\":{{\"id\":\"{orgId}\",\"login\":\"{login}\"}}}}";

        _githubClientMock
            .Setup(m => m.PostGraphQLWithPaginationAsync(
                url,
                It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)),
                It.IsAny<Func<JObject, JArray>>(),
                It.IsAny<Func<JObject, JObject>>(),
                It.IsAny<int>(),
                null,
                null))
                .Returns(Array.Empty<JToken>().ToAsyncEnumerable());

        // Act
        var result = await _githubApi.GetMannequinsByLogin(orgId, login);

        // Assert
        result.Count().Should().Be(0);
    }

    [Fact]
    public async Task GetMannequinsByLogin_Returns_Mannequins()
    {
        // Arrange
        const string orgId = "ORG_ID";
        const string login = "monalisa";

        var url = $"https://api.github.com/graphql";

        var payload =
@"{""query"":""query($id: ID!, $first: Int, $after: String, $login: String) { 
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after, login: $login) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            nodes {
                                login
                                id
                                claimant {
                                    login
                                    id
                                }
                            }
                        }
                    }
                }
            }""" +
$",\"variables\":{{\"id\":\"{orgId}\",\"login\":\"{login}\"}}}}";

        var mannequin = new
        {
            login,
            id = "DUMMYID1",
            claimant = new { }
        };

        _githubClientMock
            .Setup(m => m.PostGraphQLWithPaginationAsync(
                url,
                It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)),
                It.IsAny<Func<JObject, JArray>>(),
                It.IsAny<Func<JObject, JObject>>(),
                It.IsAny<int>(),
                null,
                null))
                .Returns(new[]
                    {
                        JToken.FromObject(mannequin),
                    }.ToAsyncEnumerable());

        // Act
        var result = await _githubApi.GetMannequinsByLogin(orgId, login);

        // Assert
        result.Count().Should().Be(1);

        var mannequinsResult = result.ToArray();
        mannequinsResult[0].Id.Should().Be("DUMMYID1");
        mannequinsResult[0].Login.Should().Be(login);
        mannequinsResult[0].MappedUser.Should().BeNull();
    }

    [Fact]
    public async Task CreateAttributionInvitation_Returns_Success()
    {
        // Arrange
        const string orgId = "dummyorgid";
        const string mannequinId = "NDQ5VXNlcjc4NDc5MzU=";
        const string mannequinUser = "mona";
        const string targetUserId = "ND5TVXNlcjc4NDc5MzU=";
        const string targetUser = "lisa";

        var url = $"https://api.github.com/graphql";

        var payload = @"{""query"":""mutation($orgId: ID!,$sourceId: ID!,$targetId: ID!) { createAttributionInvitation(
		            input: { ownerId: $orgId, sourceId: $sourceId, targetId: $targetId }
	            ) {
		            source {
			            ... on Mannequin {
				            id
				            login
			            }
		            }

		            target {
			            ... on User {
				            id
				            login
			            }
		            }
                }
            }""" + $",\"variables\":{{\"orgId\":\"{orgId}\", \"sourceId\":\"{mannequinId}\", \"targetId\":\"{targetUserId}\"}}}}";

        var response = $@"{{
                ""data"": {{
                    ""createAttributionInvitation"": {{
                        ""source"": {{
                            ""id"": ""{mannequinId}"",
                            ""login"": ""{mannequinUser}""
                        }},
                        ""target"": {{
                            ""id"": ""{targetUserId}"",
                            ""login"": ""{targetUser}""
                        }}
                    }}
                }}
            }}";

        var expectedCreateAttributionInvitationResponse = new CreateAttributionInvitationResult()
        {
            Data = new CreateAttributionInvitationData()
            {
                CreateAttributionInvitation = new CreateAttributionInvitation()
                {
                    Source = new UserInfo()
                    {
                        Id = mannequinId,
                        Login = mannequinUser
                    },
                    Target = new UserInfo()
                    {
                        Id = targetUserId,
                        Login = targetUser
                    }
                }
            }
        };

        _githubClientMock
            .Setup(m => m.PostAsync(url,
            It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)), null))
                .ReturnsAsync(response);

        // Act
        var result = await _githubApi.CreateAttributionInvitation(orgId, mannequinId, targetUserId);

        // Assert
        result.Should().BeEquivalentTo(expectedCreateAttributionInvitationResponse);
    }

    [Fact]
    public async Task ReclaimMannequinSkipInvitation_Returns_Error_When_Target_Not_Member()
    {
        // Arrange
        const string orgId = "ORG_ID";
        const string mannequinId = "NDQ5VXNlcjc4NDc5MzU=";
        const string targetUserId = "NDQ5VXNlcjc4NDc5MzU=";
        const string url = "https://api.github.com/graphql";

        var payload = @"{""query"":""mutation($orgId: ID!,$sourceId: ID!,$targetId: ID!) { reattributeMannequinToUser(
                    input: { ownerId: $orgId, sourceId: $sourceId, targetId: $targetId }
                ) {
                    source {
                        ... on Mannequin {
                            id
                            login
                        }
                    }

                    target {
                        ... on User {
                            id
                            login
                        }
                    }
                }
            }""" + $",\"variables\":{{\"orgId\":\"{orgId}\", \"sourceId\":\"{mannequinId}\", \"targetId\":\"{targetUserId}\"}}}}";

        const string errorMessage = "Target must be a member";

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => Compact(x.ToJson()) == Compact(payload)), null))
            .ThrowsAsync(new OctoshiftCliException(errorMessage));

        // Act
        var result = await _githubApi.ReclaimMannequinSkipInvitation(orgId, mannequinId, targetUserId);

        // Assert
        result.Data.Should().BeNull();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().ContainSingle();
        result.Errors.First().Message.Should().Be(errorMessage);
    }

    [Fact]
    public async Task StartMetadataArchiveGeneration_Returns_The_Initiated_Migration_Id()
    {
        // Arrange
        const string url = $"https://api.github.com/orgs/{GITHUB_ORG}/migrations";
        var payload = new
        {
            repositories = new[] { GITHUB_REPO },
            exclude_git_data = true,
            exclude_releases = false,
            lock_repositories = false,
            exclude_owner_projects = true
        };
        const int expectedMigrationId = 1;
        var response = new { id = expectedMigrationId };

        _githubClientMock
            .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response.ToJson());

        // Act
        var actualMigrationId = await _githubApi.StartMetadataArchiveGeneration(GITHUB_ORG, GITHUB_REPO, false, false);

        // Assert
        actualMigrationId.Should().Be(expectedMigrationId);
    }

    [Fact]
    public async Task StartMetadataArchiveGeneration_Excludes_Releases_When_Skip_Releases_Is_True()
    {
        // Arrange
        const string url = $"https://api.github.com/orgs/{GITHUB_ORG}/migrations";
        var payload = new
        {
            repositories = new[] { GITHUB_REPO },
            exclude_git_data = true,
            exclude_releases = true,
            lock_repositories = false,
            exclude_owner_projects = true
        };
        var response = new { id = 1 };

        _githubClientMock.Setup(m => m.PostAsync(url, It.IsAny<object>(), null)).ReturnsAsync(response.ToJson());

        // Act
        await _githubApi.StartMetadataArchiveGeneration(GITHUB_ORG, GITHUB_REPO, true, false);

        // Assert
        _githubClientMock.Verify(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task StartMetadataArchiveGeneration_Locks_Repos_When_Lock_Source_Repo_Is_True()
    {
        // Arrange
        const string url = $"https://api.github.com/orgs/{GITHUB_ORG}/migrations";
        var payload = new
        {
            repositories = new[] { GITHUB_REPO },
            exclude_git_data = true,
            exclude_releases = true,
            lock_repositories = true,
            exclude_owner_projects = true
        };
        var response = new { id = 1 };

        _githubClientMock.Setup(m => m.PostAsync(url, It.IsAny<object>(), null)).ReturnsAsync(response.ToJson());

        // Act
        await _githubApi.StartMetadataArchiveGeneration(GITHUB_ORG, GITHUB_REPO, true, true);

        // Assert
        _githubClientMock.Verify(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task StartGitArchiveGeneration_Returns_The_Initiated_Migration_Id()
    {
        // Arrange
        const string url = $"https://api.github.com/orgs/{GITHUB_ORG}/migrations";
        var payload = new
        {
            repositories = new[] { GITHUB_REPO },
            exclude_metadata = true
        };
        const int expectedMigrationId = 1;
        var response = new { id = expectedMigrationId };

        _githubClientMock
            .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
            .ReturnsAsync(response.ToJson());

        // Act
        var actualMigrationId = await _githubApi.StartGitArchiveGeneration(GITHUB_ORG, GITHUB_REPO);

        // Assert
        actualMigrationId.Should().Be(expectedMigrationId);
    }

    [Fact]
    public async Task StartGitArchiveGeneration_Throws_Octoshift_CLI_Exception_When_Blob_Storage_Settings_Are_Not_Set()
    {
        // Arrange
        const string url = $"https://api.github.com/orgs/{GITHUB_ORG}/migrations";
        var payload = new
        {
            repositories = new[] { GITHUB_REPO },
            exclude_metadata = true
        };
        var exception_message = "Before you can start a migration, you must configure blob storage settings in your management console.";

        _githubClientMock
            .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null))
                .ThrowsAsync(new HttpRequestException(exception_message, null, HttpStatusCode.BadGateway));

        // Act
        await _githubApi.Invoking(api => api.StartGitArchiveGeneration(GITHUB_ORG, GITHUB_REPO))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage(exception_message);
    }


    [Fact]
    public async Task GetSecretScanningAlertsData()
    {
        // Arrange
        const string url =
            $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts?per_page=100";

        var secretScanningAlert_1 = $@"
                {{
                    ""number"": 18,
                    ""created_at"": ""2022-05-05T21:40:00Z"",
                    ""updated_at"": ""2022-05-05T21:40:00Z"",
                    ""url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/18"",
                    ""html_url"": ""https://github.com/{GITHUB_ORG}/{GITHUB_REPO}/security/secret-scanning/18"",
                    ""locations_url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/18/locations"",
                    ""state"": ""open"",
                    ""secret_type"": ""mcantu_pattern"",
                    ""secret_type_display_name"": ""mcantu pattern"",
                    ""secret"": ""my_secret_pattern_129"",
                    ""resolution"": null,
                    ""resolved_by"": null,
                    ""resolved_at"": null,
                    ""push_protection_bypassed"": false,
                    ""push_protection_bypassed_by"": null,
                    ""push_protection_bypassed_at"": null
                }}
            ";

        var secretScanningAlert_2 = $@"
                {{
                    ""number"": 15,
                    ""created_at"": ""2021-12-21T16:41:07Z"",
                    ""updated_at"": ""2022-04-05T20:57:03Z"",
                    ""url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/15"",
                    ""html_url"": ""https://github.com/{GITHUB_ORG}/{GITHUB_REPO}/security/secret-scanning/15"",
                    ""locations_url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/15/locations"",
                    ""state"": ""resolved"",
                    ""secret_type"": ""mcantu_pattern"",
                    ""secret_type_display_name"": ""mcantu pattern"",
                    ""secret"": ""my_secret_pattern_124"",
                    ""resolution"": null,
                    ""resolved_by"": {{
                        ""login"": ""leftrightleft"",
                        ""id"": 4910518,
                        ""node_id"": ""MDQ6VXNlcjQ5MTA1MTg="",
                        ""avatar_url"": ""https://avatars.githubusercontent.com/u/4910518?v=4"",
                        ""gravatar_id"": """",
                        ""url"": ""https://api.github.com/users/leftrightleft"",
                        ""html_url"": ""https://github.com/leftrightleft"",
                        ""followers_url"": ""https://api.github.com/users/leftrightleft/followers"",
                        ""received_events_url"": ""https://api.github.com/users/leftrightleft/received_events"",
                        ""type"": ""User"",
                        ""site_admin"": true
                    }},
                    ""resolved_at"": ""2022-04-05T20:57:03"",
                    ""push_protection_bypassed"": false,
                    ""push_protection_bypassed_by"": null,
                    ""push_protection_bypassed_at"": null
                }}
            ";

        var secretScanningAlert_3 = $@"
                {{
                    ""number"": 1,
		            ""created_at"": ""2020-04-17T03:30:33Z"",
                    ""updated_at"": ""2021-10-01T15:27:33Z"",
                    ""url"": ""https://api.github.com/repos/octodemo/demo-vulnerabilities-ghas/secret-scanning/alerts/1"",
                    ""html_url"": ""https://github.com/octodemo/demo-vulnerabilities-ghas/security/secret-scanning/1"",
                    ""locations_url"": ""https://api.github.com/repos/octodemo/demo-vulnerabilities-ghas/secret-scanning/alerts/1/locations"",
                    ""state"": ""open"",
                    ""secret_type"": ""github_personal_access_token"",
                    ""secret_type_display_name"": ""GitHub Personal Access Token"",
                    ""secret"": ""fb5xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx550b82"",
                    ""resolution"": null,
		            ""resolved_by"": null,
		            ""resolved_at"": null,
		            ""push_protection_bypassed"": false,
		            ""push_protection_bypassed_by"": null,
		            ""push_protection_bypassed_at"": null
	            }}
            ";

        var secretScanningAlert_4 = $@"
                {{
                    ""number"": 1,
		            ""created_at"": ""2022-08-10T07:58:30Z"",
                    ""updated_at"": ""2022-08-15T13:53:42Z"",
                    ""url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/1"",
                    ""html_url"": ""https://github.com/{GITHUB_ORG}/{GITHUB_REPO}/security/secret-scanning/1"",
                    ""locations_url"": ""https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/1/locations"",
                    ""state"": ""resolved"",
                    ""secret_type"": ""google_api_key"",
                    ""secret_type_display_name"": ""Google API Key"",
                    ""secret"": ""AIzaSyAxxxxxxxxxxxxxxxxxxxxxxxxxxxxt2Q"",
                    ""resolution"": ""false_positive"",
                    ""resolved_by"": {{
                        ""login"": ""peter-murray"",
                        ""id"": 681306,
                        ""node_id"": ""MDQ6VXNlcjY4MTMwNg=="",
                        ""avatar_url"": ""https://avatars.githubusercontent.com/u/681306?v=4"",
                        ""gravatar_id"": """",
                        ""url"": ""https://api.github.com/users/peter-murray"",
                        ""html_url"": ""https://github.com/peter-murray"",
                        ""followers_url"": ""https://api.github.com/users/peter-murray/followers"",
                        ""following_url"": ""https://api.github.com/users/peter-murray/following{{/other_user}}"",
                        ""gists_url"": ""https://api.github.com/users/peter-murray/gists{{/gist_id}}"",
                        ""starred_url"": ""https://api.github.com/users/peter-murray/starred{{/owner}}{{/repo}}"",
                        ""subscriptions_url"": ""https://api.github.com/users/peter-murray/subscriptions"",
                        ""organizations_url"": ""https://api.github.com/users/peter-murray/orgs"",
                        ""repos_url"": ""https://api.github.com/users/peter-murray/repos"",
                        ""events_url"": ""https://api.github.com/users/peter-murray/events{{/privacy}}"",
                        ""received_events_url"": ""https://api.github.com/users/peter-murray/received_events"",
                        ""type"": ""User"",
                        ""site_admin"": true
                    }},
                    ""resolved_at"": ""2022-08-15T13:53:42Z"",
                    ""push_protection_bypassed"": false,
		            ""push_protection_bypassed_by"": null,
		            ""push_protection_bypassed_at"": null
	            }}
            ";

        var responsePage1 = $@"
                [
                    {secretScanningAlert_1},
                    {secretScanningAlert_2},
                ]
            ";

        var responsePage2 = $@"
                [
                    {secretScanningAlert_3},
                    {secretScanningAlert_4},
                ]
            ";

        async IAsyncEnumerable<JToken> GetAllPages()
        {
            var jArrayPage1 = JArray.Parse(responsePage1);
            yield return jArrayPage1[0];
            yield return jArrayPage1[1];

            var jArrayPage2 = JArray.Parse(responsePage2);
            yield return jArrayPage2[0];
            yield return jArrayPage2[1];

            await Task.CompletedTask;
        }

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(GetAllPages);

        // Act
        var scanResults = await _githubApi.GetSecretScanningAlertsForRepository(GITHUB_ORG, GITHUB_REPO);

        // Assert
        scanResults.Count().Should().Be(4);
    }

    [Fact]
    public async Task GetSecretScanningAlertLocationData()
    {
        // Arrange
        const int alert = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alert}/locations?per_page=100";

        var alertLocation_1 = $@"
                {{
                    ""type"": ""commit"",
                    ""details"": {{
                        ""path"": ""src/test/java/com/github/demo/service/BookServiceTest.java"",
                        ""start_line"": 18,
                        ""end_line"": 18,
                        ""start_column"": 46,
                        ""end_column"": 85,
                        ""blob_sha"": ""c9aaf762f939a1e5378d4c5b9584d64672f1069f"",
                        ""blob_url"": ""https://api.github.com/repos/octodemo/pm-ghas-demo/git/blobs/c9aaf762f939a1e5378d4c5b9584d64672f1069f"",
                        ""commit_sha"": ""5b4678d6a8986edf1fbc878ab90bd5efc35ef9db"",
                        ""commit_url"": ""https://api.github.com/repos/octodemo/pm-ghas-demo/git/commits/5b4678d6a8986edf1fbc878ab90bd5efc35ef9db""
                    }}
                }}
            ";

        var alertLocation_2 = $@"
                {{
                    ""type"": ""commit"",
                    ""details"": {{
                        ""path"": ""src/index.js"",
                        ""start_line"": 5,
                        ""end_line"": 5,
                        ""start_column"": 12,
                        ""end_column"": 52,
                        ""blob_sha"": ""2044bb6ccd535142b974776db108c32a19f89e80"",
                        ""blob_url"": ""https://api.github.com/repos/octodemo/demo-vulnerabilities-ghas/git/blobs/2044bb6ccd535142b974776db108c32a19f89e80"",
                        ""commit_sha"": ""c8d00bc80bad56d21bcec32a2a7b74c115dd7bc7"",
                        ""commit_url"": ""https://api.github.com/repos/octodemo/demo-vulnerabilities-ghas/git/commits/c8d00bc80bad56d21bcec32a2a7b74c115dd7bc7""
                    }}
                }}
            ";

        var responsePage1 = $@"
                [
                    {alertLocation_1},
                ]
            ";

        var responsePage2 = $@"
                [
                    {alertLocation_2},
                ]
            ";

        async IAsyncEnumerable<JToken> GetAllPages()
        {
            var jArrayPage1 = JArray.Parse(responsePage1);
            yield return jArrayPage1[0];

            var jArrayPage2 = JArray.Parse(responsePage2);
            yield return jArrayPage2[0];

            await Task.CompletedTask;
        }

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(GetAllPages);

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alert);

        // Assert
        locations.Count().Should().Be(2);
        var locationsArray = locations.ToArray();

        var location = locationsArray[0];
        var expectedData = JObject.Parse(alertLocation_1);
        location.Path.Should().Be((string)expectedData["details"]["path"]);

        location = locationsArray[1];
        expectedData = JObject.Parse(alertLocation_2);
        location.Path.Should().Be((string)expectedData["details"]["path"]);
    }

    [Fact]
    public async Task UpdateSecretScanningAlert_Calls_The_Right_Endpoint_With_Payload_For_Resolved_State()
    {
        // Arrange
        const int alertNumber = 100;
        const string alertState = "resolved";
        const string alertResolution = "wont_fix";
        const string alertResolutionComment = "This is a false positive";

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}";
        var payload = new
        {
            state = alertState,
            resolution = alertResolution,
            resolution_comment = alertResolutionComment
        };

        // Act
        await _githubApi.UpdateSecretScanningAlert(GITHUB_ORG, GITHUB_REPO, alertNumber, alertState, alertResolution, alertResolutionComment);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task UpdateSecretScanningAlert_Calls_The_Right_Endpoint_With_Payload_For_Open_State()
    {
        // Arrange
        const int alertNumber = 1;
        const string alertState = "open";

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}";
        var payload = new { state = alertState };

        // Act
        await _githubApi.UpdateSecretScanningAlert(GITHUB_ORG, GITHUB_REPO, alertNumber, alertState);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task GetDefaultBranch_Returns_Default_Branch_Field()
    {
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}";

        var response = $@"
            {{
                ""default_branch"": ""main"" 
            }}";

        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        var result = await _githubApi.GetDefaultBranch(GITHUB_ORG, GITHUB_REPO);

        result.Should().Be("main");
    }

    [Fact]
    public async Task GetCodeScanningAnalysisForRepository_Returns_Analyses()
    {
        // Arrange
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/analyses?per_page=100&sort=created&direction=asc";

        var analysis1 = $@"
                {{
                    ""ref"": ""refs/heads/sg-tfsec-test"",
                    ""commit_sha"": ""25cb837876685f98756d0c934ffe6cd09da570f8"",
                    ""created_at"": ""2022-08-08T19:00:18Z"",
                    ""id"": 38200197,
                }}
            ";

        var analysis2 = $@"
                {{
                    ""ref"": ""refs/heads/main"",
                    ""commit_sha"": ""67f8626e1f3ca40e9678e1dcfc4f840009ffc260"",
                    ""created_at"": ""2022-08-06T19:40:39Z"",
                    ""id"": 38026365,
                }}
            ";

        var analysis3 = $@"
                {{
                    ""ref"": ""refs/heads/main"",
                    ""commit_sha"": ""67f8626e1f3ca40e9678e1dcfc4f840009ffc260"",
                    ""created_at"": ""2022-08-06T19:30:25Z"",
                    ""id"": 38025984,
                }}
            ";

        var analyses = new List<JToken> { JToken.Parse(analysis1), JToken.Parse(analysis2), JToken.Parse(analysis3) };

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(analyses.ToAsyncEnumerable());

        // Act
        var scanResults = await _githubApi.GetCodeScanningAnalysisForRepository(GITHUB_ORG, GITHUB_REPO);

        // Assert
        scanResults.Count().Should().Be(3);

        var expectedData = JObject.Parse(analysis1);
        scanResults.ElementAt(0).Id.Should().Be((int)expectedData["id"]);
        scanResults.ElementAt(0).Ref.Should().Be((string)expectedData["ref"]);
        scanResults.ElementAt(0).CommitSha.Should().Be((string)expectedData["commit_sha"]);
        scanResults.ElementAt(0).CreatedAt.Should().Be((string)expectedData["created_at"]);

        expectedData = JObject.Parse(analysis2);
        scanResults.ElementAt(1).Id.Should().Be((int)expectedData["id"]);
        scanResults.ElementAt(1).Ref.Should().Be((string)expectedData["ref"]);
        scanResults.ElementAt(1).CommitSha.Should().Be((string)expectedData["commit_sha"]);
        scanResults.ElementAt(1).CreatedAt.Should().Be((string)expectedData["created_at"]);

        expectedData = JObject.Parse(analysis3);
        scanResults.ElementAt(2).Id.Should().Be((int)expectedData["id"]);
        scanResults.ElementAt(2).Ref.Should().Be((string)expectedData["ref"]);
        scanResults.ElementAt(2).CommitSha.Should().Be((string)expectedData["commit_sha"]);
        scanResults.ElementAt(2).CreatedAt.Should().Be((string)expectedData["created_at"]);
    }

    [Fact]
    public async Task GetCodeScanningAnalysisForRepository_Passes_Filtered_Branch_As_QueryString()
    {
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/analyses?per_page=100&sort=created&direction=asc&ref=main";

        var analysis = $@"
                {{
                    ""ref"": ""refs/heads/main"",
                    ""commit_sha"": ""67f8626e1f3ca40e9678e1dcfc4f840009ffc260"",
                    ""created_at"": ""2022-08-06T19:40:39Z"",
                    ""id"": 38026365,
                }}
            ";

        var analyses = new List<JToken> { JToken.Parse(analysis) };
        _githubClientMock.Setup(m => m.GetAllAsync(url, null)).Returns(analyses.ToAsyncEnumerable());

        await _githubApi.GetCodeScanningAnalysisForRepository(GITHUB_ORG, GITHUB_REPO, "main");
        _githubClientMock.Verify(m => m.GetAllAsync(url, null));
    }

    [Fact]
    public async Task GetCodeScanningAnalysisForRepository_Returns_Empty_List_When_404()
    {
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/analyses?per_page=100&sort=created&direction=asc&ref=main";

        _githubClientMock.Setup(m => m.GetAllAsync(url, null)).Throws(new HttpRequestException("blah blah no analysis found", null, HttpStatusCode.NotFound));

        var result = await _githubApi.GetCodeScanningAnalysisForRepository(GITHUB_ORG, GITHUB_REPO, "main");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCodeScanningAlertsForRepository_Returns_Correct_Data()
    {
        // Arrange
        const string url =
            $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/alerts?per_page=100&sort=created&direction=asc";

        var codeScanningAlert_1 = $@"
                {{
                    ""number"": 1,
                    ""url"": ""https://api.github.com/repos/Braustuben/gei-import-test-repo/code-scanning/alerts/3"",
                    ""state"": ""fixed"",
                    ""dismissed_at"": null,
                    ""dismissed_reason"": null,
                    ""dismissed_comment"": null,
                    ""rule"": {{
                      ""id"": ""java/zipslip"",
                    }},
                    ""most_recent_instance"": {{
                      ""ref"": ""refs/heads/main"",
                      ""commit_sha"": ""d80eeb44bb13ebd76ee6fdf61d0245c6c341152f"",
                      ""location"": {{
                        ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                        ""start_line"": 161,
                        ""end_line"": 161,
                        ""start_column"": 51,
                        ""end_column"": 56
                      }},
                    }},
                  }}
                ";

        var codeScanningAlert_2 = $@"
                {{
                    ""number"": 2,
                    ""url"": ""https://api.github.com/repos/Braustuben/gei-import-test-repo/code-scanning/alerts/2"",
                    ""state"": ""dismissed"",
                    ""dismissed_at"": ""2022-07-25T06:09:14Z"",
                    ""dismissed_reason"": ""won't fix"",
                    ""dismissed_comment"": ""Comment saying why this won't be fixed."",
                    ""rule"": {{
                      ""id"": ""java/sql-injection"",
                    }},
                    ""most_recent_instance"": {{
                      ""ref"": ""refs/heads/main"",
                      ""commit_sha"": ""4f8ecaaca41c4121a07fbc9d1bc8e69a1f2271dc"",
                      ""location"": {{
                        ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                        ""start_line"": 120,
                        ""end_line"": 120,
                        ""start_column"": 42,
                        ""end_column"": 47
                      }},
                    }},
                  }}
                ";

        var codeScanningAlert_3 = $@"
                 {{
                    ""number"": 3,
                    ""url"": ""https://api.github.com/repos/Braustuben/gei-import-test-repo/code-scanning/alerts/1"",
                    ""state"": ""fixed"",
                    ""dismissed_at"": ""2022-07-15T07:58:06Z"",
                    ""dismissed_reason"": ""used in tests"",
                    ""dismissed_comment"": ""Closed again"",
                    ""rule"": {{
                      ""id"": ""java/sql-injection"",
                    }},
                    ""most_recent_instance"": {{
                      ""ref"": ""refs/heads/main"",
                      ""commit_sha"": ""b42f07d50e5ce4451d599e6cc9ac46f3a03fc352"",
                      ""location"": {{
                        ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                        ""start_line"": 120,
                        ""end_line"": 120,
                        ""start_column"": 51,
                        ""end_column"": 56
                      }},
                    }},
                  }}
                ";

        var alerts = new List<JToken> { JToken.Parse(codeScanningAlert_1), JToken.Parse(codeScanningAlert_2), JToken.Parse(codeScanningAlert_3) };

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(alerts.ToAsyncEnumerable());

        // Act
        var scanResults = await _githubApi.GetCodeScanningAlertsForRepository(GITHUB_ORG, GITHUB_REPO);

        // Assert
        scanResults.Count().Should().Be(3);
        AssertCodeScanningData(scanResults.ElementAt(0), JObject.Parse(codeScanningAlert_1));
        AssertCodeScanningData(scanResults.ElementAt(1), JObject.Parse(codeScanningAlert_2));
        AssertCodeScanningData(scanResults.ElementAt(2), JObject.Parse(codeScanningAlert_3));
    }

    [Fact]
    public async Task GetCodeScanningAlertsForRepository_Passes_Branch_As_Query()
    {
        var emptyResult = Array.Empty<JToken>();
        const string url =
            $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/alerts?per_page=100&sort=created&direction=asc&ref=main";
        _githubClientMock.Setup(m => m.GetAllAsync(url, null)).Returns(emptyResult.ToAsyncEnumerable());

        await _githubApi.GetCodeScanningAlertsForRepository(GITHUB_ORG, GITHUB_REPO, "main");

        _githubClientMock.VerifyAll();
    }

    private void AssertCodeScanningData(CodeScanningAlert actual, JToken expectedData)
    {
        actual.Number.Should().Be((int)expectedData["number"]);
        actual.State.Should().Be((string)expectedData["state"]);
        actual.RuleId.Should().Be((string)expectedData["rule"]["id"]);
        actual.DismissedAt.Should().Be((string)expectedData["dismissed_at"]);
        actual.DismissedReason.Should().Be((string)expectedData["dismissed_reason"]);
        actual.DismissedComment.Should().Be((string)expectedData["dismissed_comment"]);

        AssertCodeScanningInstanceData(actual.MostRecentInstance, expectedData["most_recent_instance"]);
    }

    [Fact]
    public async Task GetCodeScanningAlertInstances_Returns_Correct_Data()
    {
        // Arrange
        const string url =
            $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/alerts/2/instances?per_page=100";

        var codeScanningAlertInstance1 = $@"
                {{
                  ""ref"": ""refs/heads/main"",
                  ""commit_sha"": ""d80eeb44bb13ebd76ee6fdf61d0245c6c341152f"",
                  ""location"": {{
                    ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                    ""start_line"": 161,
                    ""end_line"": 161,
                    ""start_column"": 51,
                    ""end_column"": 56
                  }},
                }}
            ";

        var codeScanningAlertInstance2 = $@"
                {{
                  ""ref"": ""refs/heads/main"",
                  ""commit_sha"": ""4f8ecaaca41c4121a07fbc9d1bc8e69a1f2271dc"",
                  ""location"": {{
                    ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                    ""start_line"": 120,
                    ""end_line"": 120,
                    ""start_column"": 42,
                    ""end_column"": 47
                  }},
                }}
            ";

        var codeScanningAlertInstance3 = $@"
                 {{
                  ""ref"": ""refs/heads/main"",
                  ""commit_sha"": ""b42f07d50e5ce4451d599e6cc9ac46f3a03fc352"",
                  ""location"": {{
                    ""path"": ""src/main/java/com/github/demo/service/BookDatabaseImpl.java"",
                    ""start_line"": 120,
                    ""end_line"": 120,
                    ""start_column"": 51,
                    ""end_column"": 56
                  }},
                 }}
                ";

        var instances = new List<JToken> { JToken.Parse(codeScanningAlertInstance1), JToken.Parse(codeScanningAlertInstance2), JToken.Parse(codeScanningAlertInstance3) };

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(instances.ToAsyncEnumerable());

        // Act
        var scanResults = await _githubApi.GetCodeScanningAlertInstances(GITHUB_ORG, GITHUB_REPO, 2);

        // Assert
        scanResults.Count().Should().Be(3);
        AssertCodeScanningInstanceData(scanResults.ElementAt(0), JObject.Parse(codeScanningAlertInstance1));
        AssertCodeScanningInstanceData(scanResults.ElementAt(1), JObject.Parse(codeScanningAlertInstance2));
        AssertCodeScanningInstanceData(scanResults.ElementAt(2), JObject.Parse(codeScanningAlertInstance3));
    }

    private void AssertCodeScanningInstanceData(CodeScanningAlertInstance actual, JToken expectedData)
    {
        actual.Ref.Should().Be((string)expectedData["ref"]);
        actual.CommitSha.Should().Be((string)expectedData["commit_sha"]);
        actual.Path.Should().Be((string)expectedData["location"]["path"]);
        actual.StartLine.Should().Be((int)expectedData["location"]["start_line"]);
        actual.EndLine.Should().Be((int)expectedData["location"]["end_line"]);
        actual.StartColumn.Should().Be((int)expectedData["location"]["start_column"]);
        actual.EndColumn.Should().Be((int)expectedData["location"]["end_column"]);
    }

    [Fact]
    public async Task UpdateCodeScanningAlert_Calls_The_Right_Endpoint_With_Payload_For_Open_State()
    {
        // Arrange
        const int alertNumber = 2;
        const string state = "open";

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/alerts/{alertNumber}";
        var payload = new { state };

        // Act
        await _githubApi.UpdateCodeScanningAlert(GITHUB_ORG, GITHUB_REPO, alertNumber, state);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task UpdateCodeScanningAlert_Replaces_Null_Dismissed_Comment_With_Empty_String()
    {
        // Arrange
        const int alertNumber = 2;
        const string state = "dismissed";
        const string reason = "false positive";

        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/alerts/{alertNumber}";
        var payload = new { state, dismissed_reason = reason, dismissed_comment = string.Empty };

        // Act
        await _githubApi.UpdateCodeScanningAlert(GITHUB_ORG, GITHUB_REPO, alertNumber, state, reason);

        // Assert
        _githubClientMock.Verify(m => m.PatchAsync(url, It.Is<object>(x => x.ToJson() == payload.ToJson()), null));
    }

    [Fact]
    public async Task GetSarifReport_For_Third_Party_Scanning_Tool()
    {
        // Arrange
        const int analysisId = 37019295;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/analyses/{analysisId}";

        var response = "SARIF_DATA";

        _githubClientMock
            .Setup(m => m.GetAsync(url, new Dictionary<string, string>() { { "accept", "application/sarif+json" } }))
            .ReturnsAsync(response);

        // Act
        var result = await _githubApi.GetSarifReport(GITHUB_ORG, GITHUB_REPO, analysisId);

        // Assert
        result.Should().Match(response);
    }

    [Fact]
    public async Task UploadSarif_Returns_Id_From_Response()
    {
        // Arrange
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/sarifs";

        var sarifCommitSha = "fake_commit_sha";
        var sarifRef = "refs/heads/main";
        var sarif = "fake_gzip_sarif";

        var expectedPayload = new
        {
            commit_sha = sarifCommitSha,
            sarif = StringCompressor.GZipAndBase64String(sarif),
            @ref = sarifRef
        };

        var response = $@"
                {{
                    ""id"": ""sarif-id"",
                }}  
            ";
        _githubClientMock
            .Setup(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == expectedPayload.ToJson()), null))
            .ReturnsAsync(response);

        // Act
        var actualId = await _githubApi.UploadSarifReport(GITHUB_ORG, GITHUB_REPO, sarif, sarifCommitSha, sarifRef);

        // Assert
        actualId.Should().Match("sarif-id");
    }

    [Fact]
    public async Task UploadSarif_Retries_On_502()
    {
        // Arrange
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/sarifs";

        var sarifCommitSha = "fake_commit_sha";
        var sarifRef = "refs/heads/main";
        var sarif = "fake_gzip_sarif";

        var expectedPayload = new
        {
            commit_sha = sarifCommitSha,
            sarif = StringCompressor.GZipAndBase64String(sarif),
            @ref = sarifRef
        };

        var response = $@"
                {{
                    ""id"": ""sarif-id"",
                }}  
            ";
        _githubClientMock
            .SetupSequence(m => m.PostAsync(url, It.Is<object>(x => x.ToJson() == expectedPayload.ToJson()), null))
            .ThrowsAsync(new HttpRequestException("\"message\": \"Server Error\"", null, HttpStatusCode.BadGateway))
            .ThrowsAsync(new HttpRequestException("\"message\": \"Server Error\"", null, HttpStatusCode.BadGateway))
            .ReturnsAsync(response);

        // Act
        var actualId = await _githubApi.UploadSarifReport(GITHUB_ORG, GITHUB_REPO, sarif, sarifCommitSha, sarifRef);

        // Assert
        actualId.Should().Match("sarif-id");
    }

    [Fact]
    public async Task GetSarifProcessingStatus_Returns_Processing_Status_From_Response()
    {
        // Arrange
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/sarifs/sarif-id";

        var response = $@"
                {{
                    ""analyses_url"": ""https://api.,github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/sarifs/sarif-id"",
                    ""processing_status"": ""pending""
                }}  
            ";
        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        // Act
        var actualStatus = await _githubApi.GetSarifProcessingStatus(GITHUB_ORG, GITHUB_REPO, "sarif-id");

        // Assert
        actualStatus.Status.Should().Be("pending");
        actualStatus.Errors.Count().Should().Be(0);
    }

    [Fact]
    public async Task GetSarifProcessingStatus_Returns_Errors_From_Response()
    {
        // Arrange
        const string url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/code-scanning/sarifs/sarif-id";

        var response = $@"
                {{
                    ""processing_status"": ""failed"",
                    ""errors"": [
                        ""error1"",
                        ""error2""
                    ]
                }}  
            ";
        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        // Act
        var actualStatus = await _githubApi.GetSarifProcessingStatus(GITHUB_ORG, GITHUB_REPO, "sarif-id");

        // Assert
        actualStatus.Errors.ElementAt(0).Should().Be("error1");
        actualStatus.Errors.ElementAt(1).Should().Be("error2");
        actualStatus.Errors.Count().Should().Be(2);
    }

    [Fact]
    public async Task GetEnterpriseServerVersion_Returns_Null_If_Not_Enterprise_Server()
    {
        // Arrange
        var url = "https://api.github.com/meta";
        var response = $@"
            {{
                ""verifiable_password_authentication"": true,
                ""packages"": [
                ],
                ""dependabot"": [
                ]
            }}";

        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        // Act
        var version = await _githubApi.GetEnterpriseServerVersion();

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public async Task GetEnterpriseServerVersion_Returns_Version()
    {
        // Arrange
        var url = "https://api.github.com/meta";
        var response = $@"
            {{
                ""verifiable_password_authentication"": true,
                ""packages"": [
                ],
                ""dependabot"": [
                ],
                ""installed_version"": ""3.7.0""
            }}";

        _githubClientMock
            .Setup(m => m.GetAsync(url, null))
            .ReturnsAsync(response);

        // Act
        var version = await _githubApi.GetEnterpriseServerVersion();

        // Assert
        version.Should().Be("3.7.0");
    }

    [Fact]
    public async Task AbortMigration_Returns_True_On_Success()
    {
        // Arrange
        const string url = "https://api.github.com/graphql";
        const string migrationId = "MIGRATION_ID";

        const string query = @"
                mutation abortRepositoryMigration(
                    $migrationId: ID!,
                )";
        const string gql = @"
                abortRepositoryMigration(
                    input: { 
                        migrationId: $migrationId
                    })
                   { success }";

        var payload = new
        {
            query = $"{query} {{ {gql} }}",
            variables = new
            {
                migrationId,
            },
            operationName = "abortRepositoryMigration"
        };

        const bool actualBooleanResponse = true;
        var response = JObject.Parse($@"
            {{
                ""data"": 
                    {{
                        ""abortRepositoryMigration"": 
                            {{
                                ""success"": ""{actualBooleanResponse}""
                            }}
                    }}
            }}");

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(url, It.Is<object>(x => Compact(x.ToJson()) == Compact(payload.ToJson())), null))
            .ReturnsAsync(response);

        // Act
        var expectedBooleanResponse = await _githubApi.AbortMigration(migrationId);

        // Assert
        expectedBooleanResponse.Should().Be(actualBooleanResponse);
    }

    [Fact]
    public async Task AbortMigration_Surfaces_Error_With_Incorrect_Migration_ID()
    {
        // Arrange
        const string migrationId = "1234";
        const string expectedErrorMessage = $"Invalid migration id: {migrationId}";

        _githubClientMock
            .Setup(m => m.PostGraphQLAsync(It.IsAny<string>(), It.IsAny<object>(), null))
            .ThrowsAsync(new OctoshiftCliException("Could not resolve to a node"));

        // Act, Assert
        await _githubApi.Invoking(api => api.AbortMigration(migrationId))
            .Should()
            .ThrowExactlyAsync<OctoshiftCliException>()
            .WithMessage(expectedErrorMessage);
    }

    [Fact]
    public async Task UploadArchiveToGithubStorage_Should_Upload_The_Content()
    {
        //Arange 
        const string orgDatabaseId = "1234";
        const string archiveName = "archiveName";

        // Using a MemoryStream as a valid stream implementation
        using var archiveContent = new MemoryStream(new byte[] { 1, 2, 3 });
        var expectedUri = "gei://archive/123456";

        // Mocking the Upload method on _archiveUploader to return the expected URI
        _archiveUploader
            .Setup(m => m.Upload(archiveContent, archiveName, orgDatabaseId))
            .ReturnsAsync(expectedUri);
        // Act
        var actualStringResponse = await _githubApi.UploadArchiveToGithubStorage(orgDatabaseId, archiveName, archiveContent);

        // Assert
        expectedUri.Should().Be(actualStringResponse);
    }

    [Fact]
    public async Task UploadArchiveToGithubStorage_Should_Throw_If_Archive_Content_Is_Null()
    {
        await FluentActions
            .Invoking(async () => await _githubApi.UploadArchiveToGithubStorage("12345", "foo", null))
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Missing_Fields_Gracefully()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var locationWithMissingFields = $@"
            {{
                ""type"": ""commit"",
                ""details"": {{
                    ""path"": ""src/index.js"",
                    ""start_line"": 5,
                    ""end_line"": 5,
                    ""blob_sha"": ""2044bb6ccd535142b974776db108c32a19f89e80""
                    // Missing start_column and end_column
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(locationWithMissingFields) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.Path.Should().Be("src/index.js");
        location.StartLine.Should().Be(5);
        location.EndLine.Should().Be(5);
        location.StartColumn.Should().Be(0);
        location.EndColumn.Should().Be(0);
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Extra_Fields_Gracefully()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var locationWithExtraFields = $@"
            {{
                ""type"": ""commit"",
                ""details"": {{
                    ""path"": ""src/index.js"",
                    ""start_line"": 5,
                    ""end_line"": 5,
                    ""start_column"": 12,
                    ""end_column"": 52,
                    ""blob_sha"": ""2044bb6ccd535142b974776db108c32a19f89e80"",
                    ""extra_field"": ""extra_value""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(locationWithExtraFields) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.Path.Should().Be("src/index.js");
        location.StartLine.Should().Be(5);
        location.EndLine.Should().Be(5);
        location.StartColumn.Should().Be(12);
        location.EndColumn.Should().Be(52);
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Commit_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var commitLocation = $@"
            {{
                ""type"": ""commit"",
                ""details"": {{
                    ""path"": ""storage/src/main/resources/.env"",
                    ""start_line"": 6,
                    ""end_line"": 6,
                    ""start_column"": 17,
                    ""end_column"": 49,
                    ""blob_sha"": ""40ecdbab769bc2cb0e4e2114fd6986ae1acc3df2"",
                    ""blob_url"": ""https://api.github.com/repos/ORG/REPO/git/blobs/40ecdbab769bc2cb0e4e2114fd6986ae1acc3df2"",
                    ""commit_sha"": ""b350b85436a872ccdc1a0cfa73f59264b8dbf4eb"",
                    ""commit_url"": ""https://api.github.com/repos/ORG/REPO/git/commits/b350b85436a872ccdc1a0cfa73f59264b8dbf4eb""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(commitLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.Path.Should().Be("storage/src/main/resources/.env");
        location.StartLine.Should().Be(6);
        location.EndLine.Should().Be(6);
        location.StartColumn.Should().Be(17);
        location.EndColumn.Should().Be(49);
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Pull_Request_Comment_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var prCommentLocation = $@"
            {{
                ""type"": ""pull_request_comment"",
                ""details"": {{
                    ""pull_request_comment_url"": ""https://api.github.com/repos/ORG/REPO/issues/comments/2758069588""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(prCommentLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.LocationType.Should().Be("pull_request_comment");
        location.PullRequestCommentUrl.Should().Be("https://api.github.com/repos/ORG/REPO/issues/comments/2758069588");
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Pull_Request_Body_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var prBodyLocation = $@"
            {{
                ""type"": ""pull_request_body"",
                ""details"": {{
                    ""pull_request_body_url"": ""https://api.github.com/repos/ORG/REPO/pulls/6""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(prBodyLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.LocationType.Should().Be("pull_request_body");
        location.PullRequestBodyUrl.Should().Be("https://api.github.com/repos/ORG/REPO/pulls/6");
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Issue_Title_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var issueTitleLocation = $@"
            {{
                ""type"": ""issue_title"",
                ""details"": {{
                    ""issue_title_url"": ""https://api.github.com/repos/ORG/REPO/issues/7""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(issueTitleLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.LocationType.Should().Be("issue_title");
        location.IssueTitleUrl.Should().Be("https://api.github.com/repos/ORG/REPO/issues/7");
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Issue_Body_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var issueBodyLocation = $@"
            {{
                ""type"": ""issue_body"",
                ""details"": {{
                    ""issue_body_url"": ""https://api.github.com/repos/ORG/REPO/issues/7""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(issueBodyLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.LocationType.Should().Be("issue_body");
        location.IssueBodyUrl.Should().Be("https://api.github.com/repos/ORG/REPO/issues/7");
    }

    [Fact]
    public async Task GetSecretScanningAlertsLocations_Handles_Issue_Comment_Location()
    {
        // Arrange
        const int alertNumber = 1;
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";

        var issueCommentLocation = $@"
            {{
                ""type"": ""issue_comment"",
                ""details"": {{
                    ""issue_comment_url"": ""https://api.github.com/repos/ORG/REPO/issues/comments/2758578142""
                }}
            }}
        ";

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(new[] { JToken.Parse(issueCommentLocation) }.ToAsyncEnumerable());

        // Act
        var locations = await _githubApi.GetSecretScanningAlertsLocations(GITHUB_ORG, GITHUB_REPO, alertNumber);

        // Assert
        locations.Should().HaveCount(1);
        var location = locations.First();
        location.LocationType.Should().Be("issue_comment");
        location.IssueCommentUrl.Should().Be("https://api.github.com/repos/ORG/REPO/issues/comments/2758578142");
    }

    [Fact]
    public async Task GetSecretScanningAlertsForRepository_Populates_ResolverName()
    {
        // Arrange
        const string url =
            $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts?per_page=100";

        var alertWithNoResolver = @"
            {
                ""number"": 10,
                ""state"": ""open"",
                ""secret_type"": ""pattern"",
                ""secret"": ""secret1"",
                ""resolution"": null,
                ""resolved_by"": null
            }
        ";
        var alertWithResolver = @"
            {
                ""number"": 11,
                ""state"": ""resolved"",
                ""secret_type"": ""pattern"",
                ""secret"": ""secret2"",
                ""resolution"": ""false_positive"",
                ""resolved_by"": { ""login"": ""resolverUser"" }
            }
        ";

        var alerts = new[]
        {
            JToken.Parse(alertWithNoResolver),
            JToken.Parse(alertWithResolver)
        }.ToAsyncEnumerable();

        _githubClientMock
            .Setup(m => m.GetAllAsync(url, null))
            .Returns(alerts);

        // Act
        var results = await _githubApi.GetSecretScanningAlertsForRepository(GITHUB_ORG, GITHUB_REPO);
        var array = results.ToArray();

        // Assert
        array.Should().HaveCount(2);
        array[0].ResolverName.Should().BeNull();
        array[1].ResolverName.Should().Be("resolverUser");
    }

    [Fact]
    public async Task GetSecretScanningAlertsForRepository_Populates_ResolutionComment_And_ResolverName()
    {
        // Arrange
        var url = $"https://api.github.com/repos/{GITHUB_ORG}/{GITHUB_REPO}/secret-scanning/alerts?per_page=100";
        var json = @"
        {
        ""number"": 5,
        ""state"": ""resolved"",
        ""secret_type"": ""pattern"",
        ""secret"": ""secretX"",
        ""resolution"": ""false_positive"",
        ""resolution_comment"": ""This is a test"",
        ""resolved_by"": { ""login"": ""actor"" }
        }";
        _githubClientMock
        .Setup(m => m.GetAllAsync(url, null))
        .Returns(new[] { JToken.Parse(json) }.ToAsyncEnumerable());

        // Act
        var results = await _githubApi.GetSecretScanningAlertsForRepository(GITHUB_ORG, GITHUB_REPO);
        var array = results.ToArray();

        // Assert
        array.Should().HaveCount(1);
        array[0].ResolutionComment.Should().Be("This is a test");
        array[0].ResolverName.Should().Be("actor");
    }

    private string Compact(string source) =>
        source
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\t", "")
            .Replace("\\r", "")
            .Replace("\\n", "")
            .Replace("\\t", "")
            .Replace(" ", "");
}
