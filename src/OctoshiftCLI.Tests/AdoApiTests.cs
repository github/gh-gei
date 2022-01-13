using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AdoApiTests
    {
        [Fact]
        public async Task GetUserId_Should_Return_UserId()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ coreAttributes: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetUserId();

            result.Should().Be(userId);
        }

        [Fact]
        public async Task GetUserId_Invalid_Json_Should_Throw_InvalidDataException()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ invalid: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            var sut = new AdoApi(mockClient.Object);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
        }

        [Fact]
        public async Task GetOrganizations_Should_Return_All_Orgs()
        {
            var userId = "foo";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}?api-version=5.0-preview.1";
            var accountsJson = "[{accountId: 'blah', AccountName: 'foo'}, {AccountName: 'foo2'}]";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(accountsJson);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizations(userId);

            result.Count().Should().Be(2);
            result.Should().Contain(new[] { "foo", "foo2" });
        }

        [Fact]
        public async Task GetOrganizationId_Should_Return_OrgId()
        {
            var userId = "foo";
            var adoOrg = "foo-org";
            var orgId = "blah";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}&api-version=5.0-preview.1";
            var accountsJson = "[{accountId: '" + orgId + "', accountName: '" + adoOrg + "'}, {accountName: 'foo2', accountId: 'asdf'}]";
            var response = JArray.Parse(accountsJson);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizationId(userId, adoOrg);

            result.Should().Be(orgId);
        }

        [Fact]
        public async Task GetTeamProjects_Should_Return_All_Team_Projects()
        {
            var adoOrg = "foo-org";
            var teamProject1 = "foo-tp";
            var teamProject2 = "foo-tp2";
            var endpoint = $"https://dev.azure.com/{adoOrg}/_apis/projects?api-version=6.1-preview";
            var json = "[{somethingElse: false, name: '" + teamProject1 + "'}, {id: 'sfasfasdf', name: '" + teamProject2 + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetTeamProjects(adoOrg);

            result.Count().Should().Be(2);
            result.Should().Contain(new[] { teamProject1, teamProject2 });
        }

        [Fact]
        public async Task GetRepos_Should_Not_Return_Disabled_Repos()
        {
            var adoOrg = "foo-org";
            var teamProject = "foo-tp";
            var repo1 = "foo-repo";
            var repo2 = "foo-repo2";
            var endpoint = $"https://dev.azure.com/{adoOrg}/{teamProject}/_apis/git/repositories?api-version=6.1-preview.1";
            var json = "[{isDisabled: 'true', name: 'testing'}, {isDisabled: false, name: '" + repo1 + "'}, {isDisabled: 'FALSE', name: '" + repo2 + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetRepos(adoOrg, teamProject);

            result.Count().Should().Be(2);
            result.Should().Contain(new[] { repo1, repo2 });
        }

        [Fact]
        public async Task GetGithubAppId_Should_Skip_Team_Projects_With_No_Endpoints()
        {
            var adoOrg = "foo-org";
            var githubOrg = "foo-gh-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var appId = Guid.NewGuid().ToString();

            var json = "[{type: 'GitHub', name: '" + githubOrg + "', id: '" + appId + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject1}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject2}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubAppId(adoOrg, githubOrg, teamProjects);

            result.Should().Be(appId);
        }

        [Fact]
        public async Task GetGithubAppId_Should_Return_Null_When_No_Team_Projects_Have_Endpoint()
        {
            var adoOrg = "foo-org";
            var githubOrg = "foo-gh-org";
            var teamProject1 = "foo-tp1";
            var teamProject2 = "foo-tp2";
            var teamProjects = new List<string>() { teamProject1, teamProject2 };
            var appId = Guid.NewGuid().ToString();

            var json = "[{type: 'GitHub', name: 'wrongOrg', id: '" + appId + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject1}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject2}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubAppId(adoOrg, githubOrg, teamProjects);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetGithubHandle_Should_Return_Handle()
        {
            var adoOrg = "FOO-ORG";
            var adoOrgId = "FOO-ORG-ID";
            var teamProject = "FOO-TEAMPROJECT";
            var githubToken = Guid.NewGuid().ToString();

            var handle = "FOO-LOGIN";
            var endpoint = $"https://dev.azure.com/{adoOrg}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
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
                            url = $"https://dev.azure.com/{adoOrg}/{teamProject}/_settings/boards-external-integration#",
                            routeId = "ms.vss-admin-web.project-admin-hub-route",
                            routeValues = new
                            {
                                project = teamProject,
                                adminPivot = "boards-external-integration",
                                controller = "ContributedPage",
                                action = "Execute",
                                serviceHost = $"{adoOrgId} ({adoOrg})"
                            }
                        }
                    }
                }
            }.ToJson();

            var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.github-user-data-provider\": {{ \"login\": '{handle}' }} }} }}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == JObject.Parse(payload).ToString())).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubHandle(adoOrg, adoOrgId, teamProject, githubToken);

            result.Should().Be(handle);
        }

        [Fact]
        public async Task GetBoardsGithubConnection_Should_Return_Connection_With_All_Repos()
        {
            var teamProject = "FOO-TEAMPROJECT";
            var orgId = "FOO-ORGID";
            var orgName = "FOO-ORG";
            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = $@"
{{
	""contributionIds"": [""ms.vss-work-web.azure-boards-external-connection-data-provider""],
	""dataProviderContext"": {{
		""properties"": {{
			""includeInvalidConnections"": false,
			""sourcePage"": {{
				""url"": ""https://dev.azure.com/FOO-ORG/FOO-TEAMPROJECT/_settings/work-team"",
				""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
				""routeValues"": {{
					""project"": ""{teamProject}"",
					""adminPivot"": ""work-team"",
					""controller"": ""ContributedPage"",
					""action"": ""Execute"",
					""serviceHost"": ""{orgId} ({orgName})""
				}}
			}}
		}}
	}}
}}";
            payload = JObject.Parse(payload).ToString();

            var connectionId = "foo-id";
            var endpointId = "foo-endpoint-id";
            var connectionName = "foo-name";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.azure-boards-external-connection-data-provider\": {{ \"externalConnections\": [ {{ id: '{connectionId}', serviceEndpoint: {{ id: '{endpointId}' }}, name: '{connectionName}', externalGitRepos: [ {{ id: '{repo1}' }}, {{ id: '{repo2}' }} ] }}, {{ thisIsIgnored: true }} ]  }} }} }}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetBoardsGithubConnection("FOO-ORG", "FOO-ORGID", "FOO-TEAMPROJECT");

            result.connectionId.Should().Be(connectionId);
            result.endpointId.Should().Be(endpointId);
            result.connectionName.Should().Be(connectionName);
            result.repoIds.Count().Should().Be(2);
            result.repoIds.Should().Contain(new[] { repo1, repo2 });
        }

        [Fact]
        public async Task CreateBoardsGithubEndpoint_Should_Return_EndpointId()
        {
            var orgName = "FOO-ORG";
            var teamProjectId = Guid.NewGuid().ToString();
            var githubToken = Guid.NewGuid().ToString();
            var githubHandle = "foo-handle";
            var endpointName = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/{teamProjectId}/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1";

            var payload = $@"
{{
    ""type"": ""githubboards"",
    ""url"": ""http://github.com"",
    ""authorization"": {{
        ""scheme"": ""PersonalAccessToken"",
        ""parameters"": {{
            ""accessToken"": ""{githubToken}""
        }}
    }},
    ""data"": {{
        ""GitHubHandle"": ""{githubHandle}""
    }},
    ""name"": ""{endpointName}""
}}";

            payload = JObject.Parse(payload).ToString();
            var endpointId = "foo-id";
            var json = $"{{id: '{endpointId}', name: 'something'}}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.CreateBoardsGithubEndpoint(orgName, teamProjectId, githubToken, githubHandle, endpointName);

            result.Should().Be(endpointId);
        }

        [Fact]
        public async Task AddRepoToBoardsGithubConnection_Should_Send_Correct_Payload()
        {
            var orgName = "FOO-ORG";
            var orgId = Guid.NewGuid().ToString();
            var teamProject = "FOO-TEAMPROJECT";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "FOO-CONNECTION";
            var endpointId = Guid.NewGuid().ToString();
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = $@"
{{
	""contributionIds"": [""ms.vss-work-web.azure-boards-save-external-connection-data-provider""],
	""dataProviderContext"": {{
		""properties"": {{
			""externalConnection"": {{
				""serviceEndpointId"": ""{endpointId}"",
				""connectionName"": ""{connectionName}"",
				""connectionId"": ""{connectionId}"",
				""operation"": 1,
                ""externalRepositoryExternalIds"": [
                    ""{repo1}"",""{repo2}""
                ],
				""providerKey"": ""github.com"",
				""isGitHubApp"": false
			}},
			""sourcePage"": {{
				""url"": ""https://dev.azure.com/{orgName}/{teamProject}/_settings/boards-external-integration"",
				""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
				""routeValues"": {{
					""project"": ""{teamProject}"",
					""adminPivot"": ""boards-external-integration"",
					""controller"": ""ContributedPage"",
					""action"": ""Execute"",
					""serviceHost"": ""{orgId} ({orgName})""
				}}
			}}
		}}
	}}
}}";

            payload = JObject.Parse(payload).ToString();
            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.AddRepoToBoardsGithubConnection(orgName, orgId, teamProject, connectionId, connectionName, endpointId, new List<string>() { repo1, repo2 });

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result);
        }

        [Fact]
        public async Task GetTeamProjectId_Should_Return_TeamProjectId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var teamProjectId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/_apis/projects/{teamProject}?api-version=5.0-preview.1";
            var response = $"{{id: '{teamProjectId}'}}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetTeamProjectId(org, teamProject);

            result.Should().Be(teamProjectId);
        }

        [Fact]
        public async Task GetRepoId_Should_Return_RepoId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories/{repo}?api-version=4.1";
            var response = $"{{id: '{repoId}'}}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetRepoId(org, teamProject, repo);

            result.Should().Be(repoId);
        }

        [Fact]
        public async Task GetPipelines_Should_Return_All_Pipelines()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repoId = Guid.NewGuid().ToString();
            var pipeline1 = "foo-pipe-1";
            var pipeline2 = "foo-pipe-2";

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions?repositoryId={repoId}&repositoryType=TfsGit";
            var response = $"[{{id: 'whatever', name: '{pipeline1}'}}, {{name: '{pipeline2}'}}]";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetPipelines(org, teamProject, repoId);

            result.Count().Should().Be(2);
            result.Should().Contain(new[] { pipeline1, pipeline2 });
        }

        [Fact]
        public async Task GetPipelineId_Should_Return_PipelineId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipeline = "foo-pipe";
            var pipelineId = 36383;

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions";
            var response = $"[ {{id: '123', name: 'wrong'}}, {{ id: '{pipelineId}', name: '{pipeline.ToUpper()}'}} ]";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetPipelineId(org, teamProject, pipeline);

            result.Should().Be(pipelineId);
        }

        [Fact]
        public async Task ShareServiceConnection_Should_Send_Correct_Payload()
        {
            var org = "FOO-ORG";
            var teamProject = "foo-teamproject";
            var teamProjectId = Guid.NewGuid().ToString();
            var serviceConnectionId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

            var payload = $@"
[{{
    ""name"": ""{org}-{teamProject}"",
	""projectReference"": {{
        ""id"": ""{teamProjectId}"",
		""name"": ""{teamProject}""
    }}
}}]";

            payload = JArray.Parse(payload).ToString();
            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.ShareServiceConnection(org, teamProject, teamProjectId, serviceConnectionId);

            mockClient.Verify(m => m.PatchAsync(endpoint, It.Is<string>(y => JArray.Parse(y).ToString() == payload)));
        }

        [Fact]
        public async Task GetPipeline_Should_Return_Pipeline()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipelineId = 826263;
            var branchName = "foo-branch";
            var defaultBranch = $"refs/heads/{branchName}";
            var clean = "True";

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";
            var response = $"{{ repository: {{ defaultBranch: '{defaultBranch}', clean: '{clean}', checkoutSubmodules: null }} }}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetPipeline(org, teamProject, pipelineId);

            result.DefaultBranch.Should().Be(branchName);
            result.Clean.Should().Be("true");
            result.CheckoutSubmodules.Should().Be("null");
        }

        [Fact]
        public async Task ChangePipelineRepo_Should_Send_Correct_Payload()
        {
            var org = "foo-org";
            var githubOrg = "foo-org";
            var githubRepo = "foo-repo";
            var teamProject = "foo-tp";
            var serviceConnectionId = Guid.NewGuid().ToString();
            var defaultBranch = "foo-branch";

            var pipeline = new AdoPipeline
            {
                Id = 123,
                Org = org,
                TeamProject = teamProject,
                DefaultBranch = defaultBranch,
                Clean = "true",
                CheckoutSubmodules = "false",
            };

            var oldJson = $@"
{{
    ""something"": ""foo"",
    ""somethingElse"": {{
        ""blah"": ""foo"",
        ""repository"": ""blah""
    }},
    ""repository"": {{
        ""testing"": true,
        ""moreTesting"": null
    }},
    ""oneLastThing"": false
}}";

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions/{pipeline.Id}?api-version=6.0";

            var newJson = $@"{{
  ""something"": ""foo"",
  ""somethingElse"": {{
    ""blah"": ""foo"",
    ""repository"": ""blah""
  }},
  ""repository"": {{
    ""properties"": {{
      ""apiUrl"": ""https://api.github.com/repos/{githubOrg}/{githubRepo}"",
      ""branchesUrl"": ""https://api.github.com/repos/{githubOrg}/{githubRepo}/branches"",
      ""cloneUrl"": ""https://github.com/{githubOrg}/{githubRepo}.git"",
      ""connectedServiceId"": ""{serviceConnectionId}"",
      ""defaultBranch"": ""{defaultBranch}"",
      ""fullName"": ""{githubOrg}/{githubRepo}"",
      ""manageUrl"": ""https://github.com/{githubOrg}/{githubRepo}"",
      ""orgName"": ""{githubOrg}"",
      ""refsUrl"": ""https://api.github.com/repos/{githubOrg}/{githubRepo}/git/refs"",
      ""safeRepository"": ""{githubOrg}/{githubRepo}"",
      ""shortName"": ""{githubRepo}"",
      ""reportBuildStatus"": ""true""
    }},
    ""id"": ""{githubOrg}/{githubRepo}"",
    ""type"": ""GitHub"",
    ""name"": ""{githubOrg}/{githubRepo}"",
    ""url"": ""https://github.com/{githubOrg}/{githubRepo}.git"",
    ""defaultBranch"": ""{defaultBranch}"",
    ""clean"": true,
    ""checkoutSubmodules"": false
  }},
  ""oneLastThing"": false
}}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(m => m.GetAsync(endpoint).Result).Returns(oldJson);
            var sut = new AdoApi(mockClient.Object);
            await sut.ChangePipelineRepo(pipeline, githubOrg, githubRepo, serviceConnectionId);

            mockClient.Verify(m => m.PutAsync(endpoint, JObject.Parse(newJson).ToString()));
        }

        [Fact]
        public async Task GetBoardsGithubRepoId_Should_Return_RepoId()
        {
            var orgName = "FOO-ORG";
            var orgId = Guid.NewGuid().ToString();
            var teamProject = "foo-tp";
            var teamProjectId = Guid.NewGuid().ToString();
            var endpointId = Guid.NewGuid().ToString();
            var githubOrg = "foo-github-org";
            var githubRepo = "foo-repo";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = $@"
{{
    ""contributionIds"": [
        ""ms.vss-work-web.github-user-repository-data-provider""
    ],
    ""dataProviderContext"": {{
        ""properties"": {{
            ""projectId"": ""{teamProjectId}"",
            ""repoWithOwnerName"": ""{githubOrg}/{githubRepo}"",
            ""serviceEndpointId"": ""{endpointId}"",
            ""sourcePage"": {{
                ""url"": ""https://dev.azure.com/{orgName}/{teamProject}/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {{
                    ""project"": ""{teamProject}"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""{orgId} ({orgName})""
                }}
            }}
        }}
    }}
}}";

            payload = JObject.Parse(payload).ToString();
            var repoId = Guid.NewGuid().ToString();
            var json = $@"{{dataProviders: {{ ""ms.vss-work-web.github-user-repository-data-provider"": {{ additionalProperties: {{ nodeId: '{repoId}' }} }} }} }}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetBoardsGithubRepoId(orgName, orgId, teamProject, teamProjectId, endpointId, githubOrg, githubRepo);

            result.Should().Be(repoId);
        }

        [Fact]
        public async Task CreateBoardsGithubConnection_Should_Send_Correct_Payload()
        {
            var orgName = "FOO-ORG";
            var orgId = Guid.NewGuid().ToString();
            var teamProject = "foo-tp";
            var endpointId = Guid.NewGuid().ToString();
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = $@"
{{
    ""contributionIds"": [
        ""ms.vss-work-web.azure-boards-save-external-connection-data-provider""
    ],
    ""dataProviderContext"": {{
        ""properties"": {{
            ""externalConnection"": {{
                ""serviceEndpointId"": ""{endpointId}"",
                ""operation"": 0,
                ""externalRepositoryExternalIds"": [
                    ""{repoId}""
                ],
                ""providerKey"": ""github.com"",
                ""isGitHubApp"": false
            }},
            ""sourcePage"": {{
                ""url"": ""https://dev.azure.com/{orgName}/{teamProject}/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {{
                    ""project"": ""{teamProject}"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""{orgId} ({orgName})""
                }}
            }}
        }}
    }}
}}";

            payload = JObject.Parse(payload).ToString();
            var mockClient = new Mock<AdoClient>(null, null);

            var sut = new AdoApi(mockClient.Object);
            await sut.CreateBoardsGithubConnection(orgName, orgId, teamProject, endpointId, repoId);

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result);
        }

        [Fact]
        public async Task DisableRepo_Should_Send_Correct_Payload()
        {
            var orgName = "foo-org";
            var teamProject = "foo-tp";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/{teamProject}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";

            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.DisableRepo(orgName, teamProject, repoId);

            var payload = @"{ ""isDisabled"": true }";
            payload = JObject.Parse(payload).ToString();

            mockClient.Verify(m => m.PatchAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result);
        }

        [Fact]
        public async Task GetIdentityDescriptor_Should_Return_IdentityDescriptor()
        {
            var orgName = "foo-org";
            var teamProjectId = Guid.NewGuid().ToString();
            var groupName = "foo-group";
            var identityDescriptor = "foo-id";

            var endpoint = $"https://vssps.dev.azure.com/{orgName}/_apis/identities?searchFilter=General&filterValue={groupName}&queryMembership=None&api-version=6.1-preview.1";
            var response = $@"[{{ properties: {{ LocalScopeId: {{ $value: ""wrong"" }} }}, descriptor: ""blah"" }}, {{ descriptor: ""{identityDescriptor}"", properties: {{ LocalScopeId: {{ $value: ""{teamProjectId}"" }} }} }}]";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetIdentityDescriptor(orgName, teamProjectId, groupName);

            result.Should().Be(identityDescriptor);
        }

        [Fact]
        public async Task LockRepo_Should_Send_Correct_Payload()
        {
            var orgName = "FOO-ORG";
            var teamProjectId = Guid.NewGuid().ToString();
            var repoId = Guid.NewGuid().ToString();
            var identityDescriptor = "foo-id";
            var gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/accesscontrolentries/{gitReposNamespace}?api-version=6.1-preview.1";

            var payload = $@"
{{
  ""token"": ""repoV2/{teamProjectId}/{repoId}"",
  ""merge"": true,
  ""accessControlEntries"": [
    {{
      ""descriptor"": ""{identityDescriptor}"",
      ""allow"": 0,
      ""deny"": 56828,
      ""extendedInfo"": {{
        ""effectiveAllow"": 0,
        ""effectiveDeny"": 56828,
        ""inheritedAllow"": 0,
        ""inheritedDeny"": 56828
      }}
    }}
  ]
}}
";

            payload = JObject.Parse(payload).ToString();
            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.LockRepo(orgName, teamProjectId, repoId, identityDescriptor);

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<string>(y => JObject.Parse(y).ToString() == payload)).Result);
        }
    }
}