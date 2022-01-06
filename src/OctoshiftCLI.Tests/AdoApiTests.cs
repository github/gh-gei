using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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

            var sut = new AdoApi(mockClient.Object);
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
    }
}