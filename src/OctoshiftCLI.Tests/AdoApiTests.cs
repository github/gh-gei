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
using OctoshiftCLI.Extensions;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class AdoApiTests
    {
        private const string ADO_SERVICE_URL = "https://dev.azure.com";
        private readonly OctoLogger logger = new();

        [Fact]
        public async Task GetUserId_Should_Return_UserId()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = new
            {
                coreAttributes = new
                {
                    PublicAlias = new
                    {
                        value = userId
                    }
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetUserId();

            result.Should().Be(userId);
        }

        [Fact]
        public async Task GetUserId_Invalid_Json_Should_Throw_InvalidDataException()
        {
            var endpoint = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";
            var userId = "foo";
            var userJson = new
            {
                invalid = new
                {
                    PublicAlias = new
                    {
                        value = userId
                    }
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(userJson.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await sut.GetUserId());
        }

        [Fact]
        public async Task GetOrganizations_Should_Return_All_Orgs()
        {
            var userId = "foo";
            var endpoint = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}?api-version=5.0-preview.1";
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(accountsJson.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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
            var accountsJson = new object[]
            {
                new
                {
                    accountId = orgId,
                    accountName = adoOrg
                },
                new
                {
                    accountName = "foo2",
                    accountId = "asdf"
                }
            };

            var response = JArray.Parse(accountsJson.ToJson());

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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
            var json = new object[]
            {
                new
                {
                    somethingElse = false,
                    name = teamProject1
                },
                new
                {
                    id = "sfasfasdf",
                    name = teamProject2
                }
            };
            var response = JArray.Parse(json.ToJson());

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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
            var json = new object[]
            {
                new
                {
                    isDisabled = true,
                    name = "testing"
                },
                new
                {
                    isDisabled = false,
                    name = repo1
                },
                new
                {
                    isDisabled = "FALSE",
                    name = repo2
                }
            };
            var response = JArray.Parse(json.ToJson());

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(response);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetEnabledRepos(adoOrg, teamProject);

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

            var json = new object[]
            {
                new
                {
                    type = "GitHub",
                    name = githubOrg,
                    id = appId
                }
            };
            var response = JArray.Parse(json.ToJson());

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject1}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject2}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject1}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(JArray.Parse("[]"));
            mockClient.Setup(x => x.GetWithPagingAsync($"https://dev.azure.com/{adoOrg}/{teamProject2}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4").Result).Returns(response);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetGithubAppId(adoOrg, githubOrg, teamProjects);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetGithubHandle_Should_Return_Handle()
        {
            var adoOrg = "FOO-ORG";
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
                            routeValues = new
                            {
                                project = teamProject
                            }
                        }
                    }
                }
            };

            var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.github-user-data-provider\": {{ \"login\": '{handle}' }} }} }}";

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetGithubHandle(adoOrg, teamProject, githubToken);

            result.Should().Be(handle);
        }

        [Fact]
        public async Task GetBoardsGithubConnection_Should_Return_Connection_With_All_Repos()
        {
            var teamProject = "FOO-TEAMPROJECT";
            var orgName = "FOO-ORG";
            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
                                project = teamProject
                            }
                        }
                    }
                }
            };

            var connectionId = "foo-id";
            var endpointId = "foo-endpoint-id";
            var connectionName = "foo-name";
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            var json = $"{{ \"dataProviders\": {{ \"ms.vss-work-web.azure-boards-external-connection-data-provider\": {{ \"externalConnections\": [ {{ id: '{connectionId}', serviceEndpoint: {{ id: '{endpointId}' }}, name: '{connectionName}', externalGitRepos: [ {{ id: '{repo1}' }}, {{ id: '{repo2}' }} ] }}, {{ thisIsIgnored: true }} ]  }} }} }}";

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetBoardsGithubConnection("FOO-ORG", "FOO-TEAMPROJECT");

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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.CreateBoardsGithubEndpoint(orgName, teamProjectId, githubToken, githubHandle, endpointName);

            result.Should().Be(endpointId);
        }

        [Fact]
        public async Task AddRepoToBoardsGithubConnection_Should_Send_Correct_Payload()
        {
            var orgName = "FOO-ORG";
            var teamProject = "FOO-TEAMPROJECT";
            var connectionId = Guid.NewGuid().ToString();
            var connectionName = "FOO-CONNECTION";
            var endpointId = Guid.NewGuid().ToString();
            var repo1 = "repo-1";
            var repo2 = "repo-2";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
                                repo1,
                                repo2
                            },
                            providerKey = "github.com",
                            isGitHubApp = false
                        },
                        sourcePage = new
                        {
                            routeValues = new
                            {
                                project = teamProject
                            }
                        }
                    }
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.AddRepoToBoardsGithubConnection(orgName, teamProject, connectionId, connectionName, endpointId, new List<string>() { repo1, repo2 });

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
        }

        [Fact]
        public async Task GetTeamProjectId_Should_Return_TeamProjectId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var teamProjectId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/_apis/projects/{teamProject}?api-version=5.0-preview.1";
            var response = new { id = teamProjectId };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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
            var response = new { id = repoId };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetRepoId(org, teamProject, repo);

            result.Should().Be(repoId);
        }

        [Fact]
        public async Task GetRepoId_Should_Work_On_Disabled_Repo()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories/{repo}?api-version=4.1";
            var allReposEndpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories?api-version=4.1";
            var response = new[] {
                new { name = "blah", id = Guid.NewGuid().ToString() },
                new { name = repo, id = repoId }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Throws(new HttpRequestException(null, null, HttpStatusCode.NotFound));
            mockClient.Setup(x => x.GetWithPagingAsync(allReposEndpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetRepoId(org, teamProject, repo);

            result.Should().Be(repoId);
        }

        [Fact]
        public async Task GetRepoId_Should_Ignore_Duplicate_Repo_Names()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var repo = "foo-repo";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories/{repo}?api-version=4.1";
            var allReposEndpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories?api-version=4.1";
            var response = new[] {
                new { name = repo, id = repoId },
                new { name = repo, id = Guid.NewGuid().ToString() }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Throws(new HttpRequestException(null, null, HttpStatusCode.NotFound));
            mockClient.Setup(x => x.GetWithPagingAsync(allReposEndpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetPipelines(org, teamProject, repoId);

            result.Count().Should().Be(2);
            result.Should().Contain(new[] { $"\\some-folder\\{pipeline1}", $"\\{pipeline2}" });
        }

        [Fact]
        public async Task GetPipelineId_Should_Return_PipelineId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipeline = "foo-pipe";
            var pipelineId = 36383;

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions";
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetPipelineId(org, teamProject, pipeline);

            result.Should().Be(pipelineId);
        }

        [Fact]
        public async Task GetPipelineId_With_Pipeline_Path_Should_Return_PipelineId()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipeline = "\\some-folder\\another\\foo-pipe";
            var pipelineId = 36383;

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions";
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetPipelineId(org, teamProject, pipeline);

            result.Should().Be(pipelineId);
        }

        [Fact]
        public async Task GetPipelineId_When_Duplicate_Pipeline_Name_And_Path_Should_Ignore_Second_Pipeline()
        {
            var org = "foo-org";
            var teamProject = "foo-tp";
            var pipeline = "\\some-folder\\foo-pipe";
            var pipelineId = 36383;

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions";
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response.ToJson()));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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

            var payload = new[]
            {
                new
                {
                    name = $"{org}-{teamProject}",
                    projectReference = new
                    {
                        id = teamProjectId,
                        name = teamProject
                    }
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.ShareServiceConnection(org, teamProject, teamProjectId, serviceConnectionId);

            mockClient.Verify(m => m.PatchAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())));
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
            var response = new
            {
                repository = new
                {
                    defaultBranch,
                    clean,
                    checkoutSubmodules = default(object)
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.GetAsync(endpoint).Result).Returns(response.ToJson());

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var (DefaultBranch, Clean, CheckoutSubmodules) = await sut.GetPipeline(org, teamProject, pipelineId);

            DefaultBranch.Should().Be(branchName);
            Clean.Should().Be("true");
            CheckoutSubmodules.Should().Be("null");
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
            var pipelineId = 123;
            var clean = "true";
            var checkoutSubmodules = "false";

            var oldJson = new
            {
                something = "foo",
                somethingElse = new
                {
                    blah = "foo",
                    repository = "blah"
                },
                repository = new
                {
                    testing = true,
                    moreTesting = default(string)
                },
                oneLastThing = false
            };

            var endpoint = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            var newJson = new
            {
                something = "foo",
                somethingElse = new
                {
                    blah = "foo",
                    repository = "blah"
                },
                repository = new
                {
                    properties = new
                    {
                        apiUrl = $"https://api.github.com/repos/{githubOrg}/{githubRepo}",
                        branchesUrl = $"https://api.github.com/repos/{githubOrg}/{githubRepo}/branches",
                        cloneUrl = $"https://github.com/{githubOrg}/{githubRepo}.git",
                        connectedServiceId = serviceConnectionId,
                        defaultBranch,
                        fullName = $"{githubOrg}/{githubRepo}",
                        manageUrl = $"https://github.com/{githubOrg}/{githubRepo}",
                        orgName = githubOrg,
                        refsUrl = $"https://api.github.com/repos/{githubOrg}/{githubRepo}/git/refs",
                        safeRepository = $"{githubOrg}/{githubRepo}",
                        shortName = githubRepo,
                        reportBuildStatus = true
                    },
                    id = $"{githubOrg}/{githubRepo}",
                    type = "GitHub",
                    name = $"{githubOrg}/{githubRepo}",
                    url = $"https://github.com/{githubOrg}/{githubRepo}.git",
                    defaultBranch,
                    clean,
                    checkoutSubmodules
                },
                oneLastThing = false
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(m => m.GetAsync(endpoint).Result).Returns(oldJson.ToJson());
            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.ChangePipelineRepo(org, teamProject, pipelineId, defaultBranch, clean, checkoutSubmodules, githubOrg, githubRepo, serviceConnectionId);

            mockClient.Verify(m => m.PutAsync(endpoint, It.Is<object>(y => y.ToJson() == newJson.ToJson())));
        }

        [Fact]
        public async Task GetBoardsGithubRepoId_Should_Return_RepoId()
        {
            var orgName = "FOO-ORG";
            var teamProject = "foo-tp";
            var teamProjectId = Guid.NewGuid().ToString();
            var endpointId = Guid.NewGuid().ToString();
            var githubOrg = "foo-github-org";
            var githubRepo = "foo-repo";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
                        projectId = teamProjectId,
                        repoWithOwnerName = $"{githubOrg}/{githubRepo}",
                        serviceEndpointId = endpointId,
                        sourcePage = new
                        {
                            routeValues = new
                            {
                                project = teamProject
                            }
                        }
                    }
                }
            };

            var repoId = Guid.NewGuid().ToString();
            var json = $@"{{dataProviders: {{ ""ms.vss-work-web.github-user-repository-data-provider"": {{ additionalProperties: {{ nodeId: '{repoId}' }} }} }} }}";

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(x => x.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result).Returns(json);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetBoardsGithubRepoId(orgName, teamProject, teamProjectId, endpointId, githubOrg, githubRepo);

            result.Should().Be(repoId);
        }

        [Fact]
        public async Task CreateBoardsGithubConnection_Should_Send_Correct_Payload()
        {
            var orgName = "FOO-ORG";
            var teamProject = "foo-tp";
            var endpointId = Guid.NewGuid().ToString();
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
                                project = teamProject
                            }
                        }
                    }
                }
            };

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.CreateBoardsGithubConnection(orgName, teamProject, endpointId, repoId);

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
        }

        [Fact]
        public async Task GetOrgOwner_Returns_Owner()
        {
            var orgName = "FOO-ORG";
            var ownerName = "Dave";
            var ownerEmail = "dave@gmail.com";

            var endpoint = $"https://dev.azure.com/{orgName}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            mockClient.Setup(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson()))).ReturnsAsync(json);

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            var result = await sut.GetOrgOwner(orgName);

            result.Should().Be($"{ownerName} ({ownerEmail})");
        }

        [Fact]
        public async Task DisableRepo_Should_Send_Correct_Payload()
        {
            var orgName = "foo-org";
            var teamProject = "foo-tp";
            var repoId = Guid.NewGuid().ToString();

            var endpoint = $"https://dev.azure.com/{orgName}/{teamProject}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.DisableRepo(orgName, teamProject, repoId);

            var payload = new { isDisabled = true };

            mockClient.Verify(m => m.PatchAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();

            mockClient.Setup(x => x.GetWithPagingAsync(endpoint).Result).Returns(JArray.Parse(response));

            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
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

            var payload = new
            {
                token = $"repoV2/{teamProjectId}/{repoId}",
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

            var mockClient = TestHelpers.CreateMock<AdoClient>();
            var sut = new AdoApi(mockClient.Object, ADO_SERVICE_URL, logger);
            await sut.LockRepo(orgName, teamProjectId, repoId, identityDescriptor);

            mockClient.Verify(m => m.PostAsync(endpoint, It.Is<object>(y => y.ToJson() == payload.ToJson())).Result);
        }
    }
}
