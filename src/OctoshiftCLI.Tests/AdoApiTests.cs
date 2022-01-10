using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AdoApiTests
    {
        [Fact]
        public async void GetUserId_Test()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ coreAttributes: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetUserId();

            Assert.Equal(userId, result);
        }

        [Fact]
        public async void GetUserId_InvalidResponse()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = "{ invalid: { PublicAlias: { value: \"" + userId + "\" }}}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson);

            using var sut = new AdoApi(mockClient.Object);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
        }

        [Fact]
        public async void GetOrganizations()
        {
            var userId = "foo";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}?api-version=5.0-preview.1";
            var accountsJson = "[{accountId: 'blah', AccountName: 'foo'}, {AccountName: 'foo2'}]";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(accountsJson);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizations(userId);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == "foo");
            Assert.Contains(result, x => x == "foo2");
        }

        [Fact]
        public async void GetOrganizationId()
        {
            var userId = "foo";
            var adoOrg = "foo-org";
            var orgId = "blah";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}&api-version=5.0-preview.1";
            var accountsJson = "[{accountId: '" + orgId + "', accountName: '" + adoOrg + "'}, {accountName: 'foo2', accountId: 'asdf'}]";
            var response = JArray.Parse(accountsJson);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetOrganizationId(userId, adoOrg);

            Assert.Equal("blah", result);
        }

        [Fact]
        public async void GetTeamProjects_TwoProjects()
        {
            var adoOrg = "foo-org";
            var teamProject1 = "foo-tp";
            var teamProject2 = "foo-tp2";
            var endpoint = $"https://dev.azure.com/{adoOrg}/_apis/projects?api-version=6.1-preview";
            var json = "[{somethingElse: false, name: '" + teamProject1 + "'}, {id: 'sfasfasdf', name: '" + teamProject2 + "'}]";
            var response = JArray.Parse(json);

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetTeamProjects(adoOrg);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == teamProject1);
            Assert.Contains(result, x => x == teamProject2);
        }

        [Fact]
        public async void GetRepos_ThreeReposOneDisabled()
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

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetRepos(adoOrg, teamProject);

            Assert.Equal(2, result.Count());
            Assert.Contains(result, x => x == repo1);
            Assert.Contains(result, x => x == repo2);
        }

        [Fact]
        public async void GetGithubAppId_TwoProjects()
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

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubAppId(adoOrg, githubOrg, teamProjects);

            Assert.Equal(appId, result);
        }

        [Fact]
        public async void GetGithubAppId_TwoProjectsNoMatch()
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

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubAppId(adoOrg, githubOrg, teamProjects);

            Assert.Null(result);
        }

        [Fact]
        public async void GetGithubHandle()
        {
            var endpoint = $"https://dev.azure.com/FOO-ORG/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";
            var payload = @"
{
    ""contributionIds"": [
        ""ms.vss-work-web.github-user-data-provider""
    ],
    ""dataProviderContext"": {
        ""properties"": {
            ""accessToken"": ""FOO-TOKEN"",
            ""sourcePage"": {
                ""url"": ""https://dev.azure.com/FOO-ORG/FOO-TEAMPROJECT/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {
                    ""project"": ""FOO-TEAMPROJECT"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""FOO-ORGID (FOO-ORG)""
                }
            }
        }
    }
}";
            var json = "{ \"dataProviders\": { \"ms.vss-work-web.github-user-data-provider\": { \"login\": 'FOO-LOGIN' } } }";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.PostAsync(endpoint, payload).Result).Returns(json);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubHandle("FOO-ORG", "FOO-ORGID", "FOO-TEAMPROJECT", "FOO-TOKEN");

            Assert.Equal("FOO-LOGIN", result);
        }

        [Fact]
        public async void GetBoardsGithubConnection()
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
            var connectionId = "foo-id";
            var endpointId = "foo-endpoint-id";
            var connectionName = "foo-name";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.azure-boards-external-connection-data-provider\": {{ \"externalConnections\": [ {{ id: '{connectionId}', serviceEndpoint: {{ id: '{endpointId}' }}, name: '{connectionName}', externalGitRepos: [ {{ id: '{repo1}' }}, {{ id: '{repo2}' }} ] }}, {{ thisIsIgnored: true }} ]  }} }} }}";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.PostAsync(endpoint, payload).Result).Returns(json);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetBoardsGithubConnection("FOO-ORG", "FOO-ORGID", "FOO-TEAMPROJECT");

            Assert.Equal(connectionId, result.connectionId);
            Assert.Equal(endpointId, result.endpointId);
            Assert.Equal(connectionName, result.connectionName);
            Assert.Equal(2, result.repoIds.Count());
            Assert.Contains(result.repoIds, x => x == "repo-1");
            Assert.Contains(result.repoIds, x => x == "repo-2");
        }

        [Fact]
        public async void CreateBoardsGithubEndpoint()
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

            var resultId = "foo-id";
            var json = $"{{id: '{resultId}', name: 'something'}}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.PostAsync(endpoint, payload).Result).Returns(json);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.CreateBoardsGithubEndpoint(orgName, teamProjectId, githubToken, githubHandle, endpointName);

            Assert.Equal(resultId, result);
        }

        [Fact]
        public async void AddRepoToBoardsGithubConnection()
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

            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.AddRepoToBoardsGithubConnection(orgName, orgId, teamProject, connectionId, connectionName, endpointId, new List<string>() { repo1, repo2 });

            mockClient.Verify(m => m.PostAsync(endpoint, payload).Result);
        }

        [Fact]
        public async void GetTeamProjectId()
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

            Assert.Equal(teamProjectId, result);
        }

        [Fact]
        public async void GetRepoId()
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

            Assert.Equal(repoId, result);
        }

        [Fact]
        public async void GetPipelines()
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

            Assert.Equal(2, result.Count());
            Assert.Contains(pipeline1, result);
            Assert.Contains(pipeline2, result);
        }

        [Fact]
        public async void GetPipelineId()
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

            Assert.Equal(pipelineId, result);
        }

        [Fact]
        public async void ShareServiceConnection()
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

            var mockClient = new Mock<AdoClient>(null, null);
            var sut = new AdoApi(mockClient.Object);
            await sut.ShareServiceConnection(org, teamProject, teamProjectId, serviceConnectionId);

            mockClient.Verify(m => m.PatchAsync(endpoint, payload));
        }

        [Fact]
        public async void GetPipeline()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipelineId = 826263;
            var defaultBranch = "refs/heads/foo-branch";
            var clean = "True";

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";
            var response = $"{{ repository: {{ defaultBranch: '{defaultBranch}', clean: '{clean}', checkoutSubmodules: null }} }}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetPipeline(org, teamProject, pipelineId);

            Assert.Equal("foo-branch", result.DefaultBranch);
            Assert.Equal("true", result.Clean);
            Assert.Equal("null", result.CheckoutSubmodules);
        }

        [Fact]
        public async void ChangePipelineRepo()
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
        public async void GetBoardsGithubRepoId()
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

            var repoId = Guid.NewGuid().ToString();
            var json = $@"{{dataProviders: {{ ""ms.vss-work-web.github-user-repository-data-provider"": {{ additionalProperties: {{ nodeId: '{repoId}' }} }} }} }}";

            var mockClient = new Mock<AdoClient>(null, null);
            mockClient.Setup(x => x.PostAsync(endpoint, payload).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetBoardsGithubRepoId(orgName, orgId, teamProject, teamProjectId, endpointId, githubOrg, githubRepo);

            Assert.Equal(repoId, result);
        }

        [Fact]
        public async void CreateBoardsGithubConnection()
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

            var mockClient = new Mock<AdoClient>(null, null);

            using var sut = new AdoApi(mockClient.Object);
            await sut.CreateBoardsGithubConnection(orgName, orgId, teamProject, endpointId, repoId);

            mockClient.Verify(m => m.PostAsync(endpoint, payload).Result);
        }

        [Fact]
        public async void DisableRepo()
        {
            var orgName = "foo-org";
            var teamProject = "foo-tp";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/{teamProject}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";
            
            var mockClient = new Mock<AdoClient>(null, null);
            using var sut = new AdoApi(mockClient.Object);
            await sut.DisableRepo(orgName, teamProject, repoId);

            var expectedPayload = @"{ ""isDisabled"": true }";

            mockClient.Verify(m => m.PatchAsync(endpoint, expectedPayload).Result);
        }

        [Fact]
        public async void GetIdentityDescriptor()
        {
            var orgName = "foo-org";
            var teamProjectId = Guid.NewGuid().ToString();
            var groupName = "foo-group";
            var identityDescriptor = "foo-id";

            var endpoint = $"https://vssps.dev.azure.com/{orgName}/_apis/identities?searchFilter=General&filterValue={groupName}&queryMembership=None&api-version=6.1-preview.1";
            var response = $@"[{{ properties: {{ LocalScopeId: {{ $value: ""wrong"" }} }}, descriptor: ""blah"" }}, {{ descriptor: ""{identityDescriptor}"", properties: {{ LocalScopeId: {{ $value: ""{teamProjectId}"" }} }} }}]";

            var mockClient = new Mock<AdoClient>(null, null);

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetIdentityDescriptor(orgName, teamProjectId, groupName);

            Assert.Equal(identityDescriptor, result);
        }

        [Fact]
        public async void LockRepo()
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

            var mockClient = new Mock<AdoClient>(null, null);
            using var sut = new AdoApi(mockClient.Object);
            await sut.LockRepo(orgName, teamProjectId, repoId, identityDescriptor);

            mockClient.Verify(m => m.PostAsync(endpoint, payload).Result);
        }
    }
}