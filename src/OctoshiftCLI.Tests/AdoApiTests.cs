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
        public async void Get_User_Id_Test()
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
        public async void Get_User_Id_Invalid_Response()
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
        public async void GetTeamProjectsTwoProjects()
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
        public async void GetReposThreeReposOneDisabled()
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
        public async void GetGithubAppIdTwoProjects()
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
        public async void GetGithubAppIdTwoProjectsNoMatch()
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

            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<StringContent>(x => x.ReadAsStringAsync().Result == payload)).Result).Returns(json);

            using var sut = new AdoApi(mockClient.Object);
            var result = await sut.GetGithubHandle("FOO-ORG", "FOO-ORGID", "FOO-TEAMPROJECT", "FOO-TOKEN");

            Assert.Equal("FOO-LOGIN", result);
        }
    }
}