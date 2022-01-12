using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI
{
    public class AdoApi
    {
        private readonly AdoClient _client;

        public AdoApi(AdoClient client) => _client = client;

        public virtual async Task<string> GetUserId()
        {
            var url = "https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=5.0-preview.1";

            var response = await _client.GetAsync(url);

            var data = JObject.Parse(response);

            var userId = (string)data.SelectToken("coreAttributes.PublicAlias.value");

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            // TODO: Throw an exception instead
            Console.WriteLine("Unexpected response when retrieving User ID");
            Console.WriteLine(response);

            throw new InvalidDataException();
        }

        public virtual async Task<IEnumerable<string>> GetOrganizations(string userId)
        {
            var url = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}?api-version=5.0-preview.1";
            var response = await _client.GetAsync(url);
            return JArray.Parse(response)
                         .Children()
                         .Select(x => (string)x["AccountName"])
                         .ToList();
        }

        public virtual async Task<string> GetOrganizationId(string userId, string adoOrganization)
        {
            var url = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId}&api-version=5.0-preview.1";

            var data = await _client.GetWithPagingAsync(url);

            // TODO: This will crash if this org doesn't exist, or the PAT doesn't have access to it
            return (string)data.Children().Single(x => ((string)x["accountName"]).ToUpper() == adoOrganization.ToUpper())["accountId"];
        }

        public virtual async Task<IEnumerable<string>> GetTeamProjects(string org)
        {
            var url = $"https://dev.azure.com/{org}/_apis/projects?api-version=6.1-preview";
            var data = await _client.GetWithPagingAsync(url);
            return data.Select(x => (string)x["name"]).ToList();
        }

        public virtual async Task<IEnumerable<string>> GetRepos(string org, string teamProject)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories?api-version=6.1-preview.1";
            var data = await _client.GetWithPagingAsync(url);
            return data.Where(x => ((string)x["isDisabled"]).ToUpperInvariant() == "FALSE")
                       .Select(x => (string)x["name"])
                       .ToList();
        }

        public virtual async Task<string> GetGithubAppId(string org, string githubOrg, IEnumerable<string> teamProjects)
        {
            if (teamProjects == null)
            {
                return null;
            }

            foreach (var adoTeamProject in teamProjects)
            {
                var url = $"https://dev.azure.com/{org}/{adoTeamProject}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4";
                var response = await _client.GetWithPagingAsync(url);

                var endpoint = response.FirstOrDefault(x => (string)x["type"] == "GitHub" && ((string)x["name"]).ToLower() == githubOrg.ToLower());

                if (endpoint != null)
                {
                    return (string)endpoint["id"];
                }
            }

            return null;
        }

        public virtual async Task<string> GetGithubHandle(string org, string orgId, string teamProject, string githubToken)
        {
            var url = $"https://dev.azure.com/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = @"
{
    ""contributionIds"": [
        ""ms.vss-work-web.github-user-data-provider""
    ],
    ""dataProviderContext"": {
        ""properties"": {
            ""accessToken"": ""GITHUB_TOKEN"",
            ""sourcePage"": {
                ""url"": ""https://dev.azure.com/ADO_ORGANIZATION/ADO_TEAMPROJECT/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {
                    ""project"": ""ADO_TEAMPROJECT"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""ADO_ORGID (ADO_ORGANIZATION)""
                }
            }
        }
    }
}";
            payload = payload.Replace("GITHUB_TOKEN", githubToken);
            payload = payload.Replace("ADO_ORGANIZATION", org);
            payload = payload.Replace("ADO_TEAMPROJECT", teamProject);
            payload = payload.Replace("ADO_ORGID", orgId);

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["dataProviders"]["ms.vss-work-web.github-user-data-provider"]["login"];
        }

        public virtual async Task<(string connectionId, string endpointId, string connectionName, IEnumerable<string> repoIds)> GetBoardsGithubConnection(string org, string orgId, string teamProject)
        {
            var url = $"https://dev.azure.com/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = @"
{
	""contributionIds"": [""ms.vss-work-web.azure-boards-external-connection-data-provider""],
	""dataProviderContext"": {
		""properties"": {
			""includeInvalidConnections"": false,
			""sourcePage"": {
				""url"": ""https://dev.azure.com/ADO_ORGANIZATION/ADO_TEAMPROJECT/_settings/work-team"",
				""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
				""routeValues"": {
					""project"": ""ADO_TEAMPROJECT"",
					""adminPivot"": ""work-team"",
					""controller"": ""ContributedPage"",
					""action"": ""Execute"",
					""serviceHost"": ""ADO_ORGID (ADO_ORGANIZATION)""
				}
			}
		}
	}
}";
            payload = payload.Replace("ADO_ORGANIZATION", org);
            payload = payload.Replace("ADO_TEAMPROJECT", teamProject);
            payload = payload.Replace("ADO_ORGID", orgId);

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            var connection = data["dataProviders"]["ms.vss-work-web.azure-boards-external-connection-data-provider"]["externalConnections"].FirstOrDefault();

            if (connection == default(JToken))
            {
                return default;
            }

            var repos = connection["externalGitRepos"].Select(x => (string)x["id"]).ToList();

            return ((string)connection["id"], (string)connection["serviceEndpoint"]["id"], (string)connection["name"], repos);
        }

        public virtual async Task<string> CreateBoardsGithubEndpoint(string org, string teamProjectId, string githubToken, string githubHandle, string endpointName)
        {
            var url = $"https://dev.azure.com/{org}/{teamProjectId}/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1";

            var payload = @"
{
    ""type"": ""githubboards"",
    ""url"": ""http://github.com"",
    ""authorization"": {
        ""scheme"": ""PersonalAccessToken"",
        ""parameters"": {
            ""accessToken"": ""GITHUB_TOKEN""
        }
    },
    ""data"": {
        ""GitHubHandle"": ""GITHUB_HANDLE""
    },
    ""name"": ""ENDPOINT_NAME""
}";

            payload = payload.Replace("GITHUB_TOKEN", githubToken);
            payload = payload.Replace("GITHUB_HANDLE", githubHandle);
            payload = payload.Replace("ENDPOINT_NAME", endpointName);

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public virtual async Task AddRepoToBoardsGithubConnection(string org, string orgId, string teamProject, string connectionId, string connectionName, string endpointId, IEnumerable<string> repoIds)
        {
            var url = $"https://dev.azure.com/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = @"
{
	""contributionIds"": [""ms.vss-work-web.azure-boards-save-external-connection-data-provider""],
	""dataProviderContext"": {
		""properties"": {
			""externalConnection"": {
				""serviceEndpointId"": ""ENDPOINT_ID"",
				""connectionName"": ""CONNECTION_NAME"",
				""connectionId"": ""CONNECTION_ID"",
				""operation"": 1,
                ""externalRepositoryExternalIds"": [
                    REPO_IDS
                ],
				""providerKey"": ""github.com"",
				""isGitHubApp"": false
			},
			""sourcePage"": {
				""url"": ""https://dev.azure.com/ADO_ORGANIZATION/ADO_TEAMPROJECT/_settings/boards-external-integration"",
				""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
				""routeValues"": {
					""project"": ""ADO_TEAMPROJECT"",
					""adminPivot"": ""boards-external-integration"",
					""controller"": ""ContributedPage"",
					""action"": ""Execute"",
					""serviceHost"": ""ADO_ORGID (ADO_ORGANIZATION)""
				}
			}
		}
	}
}";

            payload = payload.Replace("ENDPOINT_ID", endpointId);
            payload = payload.Replace("CONNECTION_NAME", connectionName);
            payload = payload.Replace("CONNECTION_ID", connectionId);
            payload = payload.Replace("ADO_ORGANIZATION", org);
            payload = payload.Replace("ADO_TEAMPROJECT", teamProject);
            payload = payload.Replace("ADO_ORGID", orgId);
            payload = payload.Replace("REPO_IDS", BuildRepoString(repoIds));

            await _client.PostAsync(url, payload);
        }

        public virtual async Task<string> GetTeamProjectId(string org, string teamProject)
        {
            var url = $"https://dev.azure.com/{org}/_apis/projects/{teamProject}?api-version=5.0-preview.1";
            var response = await _client.GetAsync(url);
            return (string)JObject.Parse(response)["id"];
        }

        public virtual async Task<string> GetRepoId(string org, string teamProject, string repo)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories/{repo}?api-version=4.1";
            var response = await _client.GetAsync(url);
            return (string)JObject.Parse(response)["id"];
        }

        public virtual async Task<IEnumerable<string>> GetPipelines(string org, string teamProject, string repoId)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions?repositoryId={repoId}&repositoryType=TfsGit";
            var response = await _client.GetWithPagingAsync(url);
            return response.Select(x => (string)x["name"]).ToList();
        }

        public virtual async Task<int> GetPipelineId(string org, string teamProject, string pipeline)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions";
            var response = await _client.GetWithPagingAsync(url);

            var result = response.Single(x => ((string)x["name"]).Trim().ToUpper() == pipeline.Trim().ToUpper());
            return (int)result["id"];
        }

        public virtual async Task ShareServiceConnection(string adoOrg, string adoTeamProject, string adoTeamProjectId, string serviceConnectionId)
        {
            var url = $"https://dev.azure.com/{adoOrg}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

            var payload = @"
[{
    ""name"": ""ADO_ORGANIZATION-ADO_TEAM_PROJECT"",
	""projectReference"": {
        ""id"": ""ADO_PROJECT_ID"",
		""name"": ""ADO_TEAM_PROJECT""
    }
}]";

            payload = payload.Replace("ADO_ORGANIZATION", adoOrg);
            payload = payload.Replace("ADO_TEAM_PROJECT", adoTeamProject);
            payload = payload.Replace("ADO_PROJECT_ID", adoTeamProjectId);

            await _client.PatchAsync(url, payload);
        }

        public virtual async Task<AdoPipeline> GetPipeline(string org, string teamProject, int pipelineId)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            var result = new AdoPipeline
            {
                Id = pipelineId,
                Org = org,
                TeamProject = teamProject
            };

            result.DefaultBranch = (string)data["repository"]["defaultBranch"];
            if (result.DefaultBranch.ToLower().StartsWith("refs/heads/"))
            {
                result.DefaultBranch = result.DefaultBranch["refs/heads/".Length..];
            }
            result.Clean = (string)data["repository"]["clean"];
            result.Clean = result.Clean == null ? "null" : result.Clean.ToLower();
            result.CheckoutSubmodules = (string)data["repository"]["checkoutSubmodules"];
            result.CheckoutSubmodules = result.CheckoutSubmodules == null ? "null" : result.CheckoutSubmodules.ToLower();

            return result;
        }

        public virtual async Task ChangePipelineRepo(AdoPipeline pipeline, string githubOrg, string githubRepo, string serviceConnectionId)
        {
            var url = $"https://dev.azure.com/{pipeline?.Org}/{pipeline?.TeamProject}/_apis/build/definitions/{pipeline?.Id}?api-version=6.0";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            var newRepo = @"
{
    ""properties"": {
        ""apiUrl"": ""https://api.github.com/repos/GITHUB_ORG/GITHUB_REPO"",
        ""branchesUrl"": ""https://api.github.com/repos/GITHUB_ORG/GITHUB_REPO/branches"",
        ""cloneUrl"": ""https://github.com/GITHUB_ORG/GITHUB_REPO.git"",
        ""connectedServiceId"": ""CONNECTED_SERVICE_ID"",
        ""defaultBranch"": ""DEFAULT_BRANCH"",
        ""fullName"": ""GITHUB_ORG/GITHUB_REPO"",
        ""manageUrl"": ""https://github.com/GITHUB_ORG/GITHUB_REPO"",
        ""orgName"": ""GITHUB_ORG"",
        ""refsUrl"": ""https://api.github.com/repos/GITHUB_ORG/GITHUB_REPO/git/refs"",
        ""safeRepository"": ""GITHUB_ORG/GITHUB_REPO"",
        ""shortName"": ""GITHUB_REPO"",
        ""reportBuildStatus"": ""true""
    },
    ""id"": ""GITHUB_ORG/GITHUB_REPO"",
    ""type"": ""GitHub"",
    ""name"": ""GITHUB_ORG/GITHUB_REPO"",
    ""url"": ""https://github.com/GITHUB_ORG/GITHUB_REPO.git"",
    ""defaultBranch"": ""DEFAULT_BRANCH"",
    ""clean"": CLEAN_FLAG,
    ""checkoutSubmodules"": CHECKOUT_SUBMODULES_FLAG
}";

            newRepo = newRepo.Replace("GITHUB_ORG", githubOrg);
            newRepo = newRepo.Replace("GITHUB_REPO", githubRepo);
            newRepo = newRepo.Replace("DEFAULT_BRANCH", pipeline?.DefaultBranch);
            newRepo = newRepo.Replace("CLEAN_FLAG", pipeline?.Clean);
            newRepo = newRepo.Replace("CHECKOUT_SUBMODULES_FLAG", pipeline?.CheckoutSubmodules);
            newRepo = newRepo.Replace("CONNECTED_SERVICE_ID", serviceConnectionId);

            var payload = new JObject();

            foreach (var prop in data.Properties())
            {
                if (prop.Name == "repository")
                {
                    prop.Value = JObject.Parse(newRepo);
                }

                payload.Add(prop.Name, prop.Value);
            }

            await _client.PutAsync(url, payload.ToString());
        }

        public virtual async Task<string> GetBoardsGithubRepoId(string org, string orgId, string teamProject, string teamProjectId, string endpointId, string githubOrg, string githubRepo)
        {
            var url = $"https://dev.azure.com/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = @"
{
    ""contributionIds"": [
        ""ms.vss-work-web.github-user-repository-data-provider""
    ],
    ""dataProviderContext"": {
        ""properties"": {
            ""projectId"": ""ADO_TEAMPROJECTID"",
            ""repoWithOwnerName"": ""GITHUB_ORG/GITHUB_REPO"",
            ""serviceEndpointId"": ""ENDPOINT_ID"",
            ""sourcePage"": {
                ""url"": ""https://dev.azure.com/ADO_ORGANIZATION/ADO_TEAMPROJECT/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {
                    ""project"": ""ADO_TEAMPROJECT"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""ADO_ORGID (ADO_ORGANIZATION)""
                }
            }
        }
    }
}";

            payload = payload.Replace("ADO_TEAMPROJECTID", teamProjectId);
            payload = payload.Replace("GITHUB_ORG", githubOrg);
            payload = payload.Replace("ENDPOINT_ID", endpointId);
            payload = payload.Replace("ADO_ORGANIZATION", org);
            payload = payload.Replace("ADO_TEAMPROJECT", teamProject);
            payload = payload.Replace("ADO_ORGID", orgId);
            payload = payload.Replace("GITHUB_REPO", githubRepo);

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["dataProviders"]["ms.vss-work-web.github-user-repository-data-provider"]["additionalProperties"]["nodeId"];
        }

        public virtual async Task CreateBoardsGithubConnection(string org, string orgId, string teamProject, string endpointId, string repoId)
        {
            var url = $"https://dev.azure.com/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

            var payload = @"
{
    ""contributionIds"": [
        ""ms.vss-work-web.azure-boards-save-external-connection-data-provider""
    ],
    ""dataProviderContext"": {
        ""properties"": {
            ""externalConnection"": {
                ""serviceEndpointId"": ""ENDPOINT_ID"",
                ""operation"": 0,
                ""externalRepositoryExternalIds"": [
                    ""REPO_ID""
                ],
                ""providerKey"": ""github.com"",
                ""isGitHubApp"": false
            },
            ""sourcePage"": {
                ""url"": ""https://dev.azure.com/ADO_ORGANIZATION/ADO_TEAMPROJECT/_settings/boards-external-integration#"",
                ""routeId"": ""ms.vss-admin-web.project-admin-hub-route"",
                ""routeValues"": {
                    ""project"": ""ADO_TEAMPROJECT"",
                    ""adminPivot"": ""boards-external-integration"",
                    ""controller"": ""ContributedPage"",
                    ""action"": ""Execute"",
                    ""serviceHost"": ""ADO_ORGID (ADO_ORGANIZATION)""
                }
            }
        }
    }
}";

            payload = payload.Replace("ENDPOINT_ID", endpointId);
            payload = payload.Replace("ADO_ORGANIZATION", org);
            payload = payload.Replace("ADO_TEAMPROJECT", teamProject);
            payload = payload.Replace("ADO_ORGID", orgId);
            payload = payload.Replace("REPO_ID", repoId);

            await _client.PostAsync(url, payload);
        }

        private string BuildRepoString(IEnumerable<string> repoIds)
        {
            var result = string.Join("\",\"", repoIds);
            return $"\"{result}\"";
        }

        public virtual async Task DisableRepo(string org, string teamProject, string repoId)
        {
            var url = $"https://dev.azure.com/{org}/{teamProject}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";

            var payload = "{ \"isDisabled\": true }";
            await _client.PatchAsync(url, payload);
        }

        public virtual async Task<string> GetIdentityDescriptor(string org, string teamProjectId, string groupName)
        {
            var url = $"https://vssps.dev.azure.com/{org}/_apis/identities?searchFilter=General&filterValue={groupName?.Replace(" ", "%20")}&queryMembership=None&api-version=6.1-preview.1";

            var identities = await _client.GetWithPagingAsync(url);

            var result = identities.Single(x => ((string)x["properties"]["LocalScopeId"]["$value"]) == teamProjectId);
            return (string)result["descriptor"];
        }

        public virtual async Task LockRepo(string org, string teamProjectId, string repoId, string identityDescriptor)
        {
            var gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";

            var url = $"https://dev.azure.com/{org}/_apis/accesscontrolentries/{gitReposNamespace}?api-version=6.1-preview.1";

            var payload = @"
{
  ""token"": ""repoV2/TEAM_PROJECT_ID/REPO_ID"",
  ""merge"": true,
  ""accessControlEntries"": [
    {
      ""descriptor"": ""IDENTITY_DESCRIPTOR"",
      ""allow"": 0,
      ""deny"": 56828,
      ""extendedInfo"": {
        ""effectiveAllow"": 0,
        ""effectiveDeny"": 56828,
        ""inheritedAllow"": 0,
        ""inheritedDeny"": 56828
      }
    }
  ]
}
";

            payload = payload.Replace("TEAM_PROJECT_ID", teamProjectId);
            payload = payload.Replace("REPO_ID", repoId);
            payload = payload.Replace("IDENTITY_DESCRIPTOR", identityDescriptor);

            await _client.PostAsync(url, payload);
        }
    }

    public class AdoPipeline
    {
        public int Id { get; set; }
        public string Org { get; set; }
        public string TeamProject { get; set; }
        public string DefaultBranch { get; set; }
        public string Clean { get; set; }
        public string CheckoutSubmodules { get; set; }
    }
}