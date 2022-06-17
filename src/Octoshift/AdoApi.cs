using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public class AdoApi
    {
        private readonly AdoClient _client;
        private readonly string _adoBaseUrl;
        private readonly OctoLogger _log;

        public AdoApi(AdoClient client, string adoServerUrl, OctoLogger log)
        {
            _client = client;
            _adoBaseUrl = adoServerUrl?.TrimEnd('/');
            _log = log;
        }

        public virtual async Task<string> GetOrgOwner(string org)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            var ownerName = (string)data["dataProviders"]["ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"]["currentOwner"]["name"];
            var ownerEmail = (string)data["dataProviders"]["ms.vss-admin-web.organization-admin-overview-delay-load-data-provider"]["currentOwner"]["email"];

            return $"{ownerName} ({ownerEmail})";
        }

        public virtual async Task<DateTime> GetLastPushDate(string org, string teamProject, string repo)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repo}/pushes?$top=1&api-version=7.1-preview.2";
            var response = await _client.GetAsync(url);

            var data = JObject.Parse(response);
            var pushDate = data.TryGetValue("value", out var dataValue) && dataValue.Any() ? (string)dataValue.First()["date"] : DateTime.MinValue.ToString();

            return DateTime.Parse(pushDate);
        }

        public virtual async Task<int> GetCommitCountSince(string org, string teamProject, string repo, DateTime fromDate)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repo}/commits?searchCriteria.fromDate={fromDate.ToShortDateString()}&api-version=7.1-preview.1";
            return await _client.GetCountUsingSkip(url);
        }

        public virtual async Task<IEnumerable<string>> GetPushersSince(string org, string teamProject, string repo, DateTime fromDate)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repo}/pushes?searchCriteria.fromDate={fromDate.ToShortDateString()}&api-version=7.1-preview.1";
            var response = await _client.GetWithPagingTopSkipAsync(url, x => $"{x["pushedBy"]["displayName"]} ({x["pushedBy"]["uniqueName"]})");

            return response;
        }

        public virtual async Task<int> GetPullRequestCount(string org, string teamProject, string repo)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repo}/pullrequests?searchCriteria.status=all&api-version=7.1-preview.1";
            var count = await _client.GetCountUsingSkip(url);
            return count;
        }

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
            var url = $"{_adoBaseUrl}/{org}/_apis/projects?api-version=6.1-preview";
            var data = await _client.GetWithPagingAsync(url);
            return data.Select(x => (string)x["name"]).ToList();
        }

        public virtual async Task<IEnumerable<string>> GetEnabledRepos(string org, string teamProject) => (await GetRepos(org, teamProject)).Where(x => !x.IsDisabled).Select(x => x.Name).ToList();

        public virtual async Task<IEnumerable<(string Id, string Name, bool IsDisabled)>> GetRepos(string org, string teamProject)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories?api-version=6.1-preview.1";
            var data = await _client.GetWithPagingAsync(url);
            return data.Select(x => ((string)x["id"], (string)x["name"], ((string)x["isDisabled"]).ToBool()))
                       .ToList();
        }

        public virtual async Task<string> GetGithubAppId(string org, string githubOrg, IEnumerable<string> teamProjects)
        {
            if (teamProjects == null)
            {
                return null;
            }

            foreach (var teamProject in teamProjects)
            {
                var appId = await GetTeamProjectGithubAppId(org, githubOrg, teamProject);
                if (appId != null)
                {
                    return appId;
                }
            }

            return null;
        }

        private async Task<string> GetTeamProjectGithubAppId(string org, string githubOrg, string teamProject)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4";
            var response = await _client.GetWithPagingAsync(url);

            var endpoint = response.FirstOrDefault(x => ((string)x["type"]).Equals("GitHub", StringComparison.OrdinalIgnoreCase) && ((string)x["name"]).Equals(githubOrg, StringComparison.OrdinalIgnoreCase));

            return endpoint != null ? (string)endpoint["id"] : null;
        }

        public virtual async Task<string> GetGithubHandle(string org, string teamProject, string githubToken)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["dataProviders"]["ms.vss-work-web.github-user-data-provider"]["login"];
        }

        public virtual async Task<(string connectionId, string endpointId, string connectionName, IEnumerable<string> repoIds)> GetBoardsGithubConnection(string org, string teamProject)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
            var url = $"{_adoBaseUrl}/{org}/{teamProjectId}/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1";

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

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public virtual async Task AddRepoToBoardsGithubConnection(string org, string teamProject, string connectionId, string connectionName, string endpointId, IEnumerable<string> repoIds)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
                            externalRepositoryExternalIds = repoIds.ToArray(),
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

            await _client.PostAsync(url, payload);
        }

        public virtual async Task<string> GetTeamProjectId(string org, string teamProject)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/projects/{teamProject}?api-version=5.0-preview.1";
            var response = await _client.GetAsync(url);
            return (string)JObject.Parse(response)["id"];
        }

        private readonly IDictionary<(string org, string teamProject), IDictionary<string, string>> _repoIds = new Dictionary<(string org, string teamProject), IDictionary<string, string>>();

        public virtual async Task<string> GetRepoId(string org, string teamProject, string repo)
        {
            org = org ?? throw new ArgumentNullException(nameof(org));
            teamProject = teamProject ?? throw new ArgumentNullException(nameof(teamProject));
            repo = repo ?? throw new ArgumentNullException(nameof(repo));

            if (!_repoIds.ContainsKey((org.ToUpper(), teamProject.ToUpper())))
            {
                var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repo}?api-version=4.1";

                try
                {
                    var response = await _client.GetAsync(url);
                    return (string)JObject.Parse(response)["id"];
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // The repo may be disabled, can still get the ID by getting it from the repo list
                    await PopulateRepoIdCache(org, teamProject);
                }
            }

            return _repoIds[(org.ToUpper(), teamProject.ToUpper())][repo.ToUpper()];
        }

        public virtual async Task PopulateRepoIdCache(string org, string teamProject)
        {
            org = org ?? throw new ArgumentNullException(nameof(org));
            teamProject = teamProject ?? throw new ArgumentNullException(nameof(teamProject));

            if (_repoIds.ContainsKey((org.ToUpper(), teamProject.ToUpper())))
            {
                return;
            }

            var ids = new Dictionary<string, string>();

            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories?api-version=4.1";

            var response = await _client.GetWithPagingAsync(url);

            foreach (var item in response)
            {
                var name = (string)item["name"];
                var id = (string)item["id"];

                var success = ids.TryAdd(name.ToUpper(), id);

                if (!success)
                {
                    _log.LogWarning($"Multiple repos with the same name were found [org: {org} project: {teamProject} repo: {name}]. Ignoring repo ID {id}");
                }
            }

            _repoIds.Add((org.ToUpper(), teamProject.ToUpper()), ids);
        }

        public virtual async Task<IEnumerable<string>> GetPipelines(string org, string teamProject, string repoId)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/build/definitions?repositoryId={repoId}&repositoryType=TfsGit&queryOrder=lastModifiedDescending";
            var response = await _client.GetWithPagingAsync(url);

            var result = response.Select(x =>
            {
                var path = (string)x["path"];
                path = path == "\\" ? string.Empty : path;
                var name = (string)x["name"];

                return $"{path}\\{name}";
            });

            return result;
        }

        private readonly IDictionary<(string org, string teamProject, string pipelinePath), int> _pipelineIds = new Dictionary<(string org, string teamProject, string pipelinePath), int>();

        public virtual async Task<int> GetPipelineId(string org, string teamProject, string pipeline)
        {
            org = org ?? throw new ArgumentNullException(nameof(org));
            teamProject = teamProject ?? throw new ArgumentNullException(nameof(teamProject));
            pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            var pipelinePath = NormalizePipelinePath(pipeline);

            if (_pipelineIds.TryGetValue((org.ToUpper(), teamProject.ToUpper(), pipelinePath.ToUpper()), out var result))
            {
                return result;
            }

            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/build/definitions";
            var response = await _client.GetWithPagingAsync(url);

            foreach (var item in response)
            {
                var path = NormalizePipelinePath((string)item["path"], (string)item["name"]);
                var id = (int)item["id"];

                var success = _pipelineIds.TryAdd((org.ToUpper(), teamProject.ToUpper(), path.ToUpper()), id);

                if (!success)
                {
                    _log.LogWarning($"Multiple pipelines with the same path/name were found [org: {org} project: {teamProject} pipeline: {path}]. Ignoring pipeline ID {id}");
                }
            }

            return _pipelineIds[(org.ToUpper(), teamProject.ToUpper(), pipelinePath.ToUpper())];
        }

        private string NormalizePipelinePath(string path, string name)
        {
            var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);

            var result = string.Join('\\', parts);

            return result.Length > 0 ? $"\\{result}\\{name}" : $"\\{name}";
        }

        private string NormalizePipelinePath(string pipeline)
        {
            var parts = pipeline.Split('\\', StringSplitOptions.RemoveEmptyEntries);

            var result = string.Join('\\', parts);
            return $"\\{result}";
        }

        public virtual async Task<bool> ContainsServiceConnection(string adoOrg, string adoTeamProject, string serviceConnectionId)
        {
            var url = $"{_adoBaseUrl}/{adoOrg}/{adoTeamProject}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

            var response = await _client.GetAsync(url);

            // When the service connection isn't shared with this team project, the response is always 'null'
            return response.HasValue() && !response.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        public virtual async Task ShareServiceConnection(string adoOrg, string adoTeamProject, string adoTeamProjectId, string serviceConnectionId)
        {
            var url = $"{_adoBaseUrl}/{adoOrg}/_apis/serviceendpoint/endpoints/{serviceConnectionId}?api-version=6.0-preview.4";

            var payload = new[]
            {
                new
                {
                    name = $"{adoOrg}-{adoTeamProject}",
                    projectReference = new
                    {
                        id = adoTeamProjectId,
                        name = adoTeamProject
                    }
                }
            };

            await _client.PatchAsync(url, payload);
        }

        public virtual async Task<(string DefaultBranch, string Clean, string CheckoutSubmodules)> GetPipeline(string org, string teamProject, int pipelineId)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            var defaultBranch = (string)data["repository"]["defaultBranch"];

            if (defaultBranch.ToLower().StartsWith("refs/heads/"))
            {
                defaultBranch = defaultBranch["refs/heads/".Length..];
            }

            var clean = (string)data["repository"]["clean"];
            clean = clean == null ? "null" : clean.ToLower();

            var checkoutSubmodules = (string)data["repository"]["checkoutSubmodules"];
            checkoutSubmodules = checkoutSubmodules == null ? "null" : checkoutSubmodules.ToLower();

            return (defaultBranch, clean, checkoutSubmodules);
        }

        public virtual async Task ChangePipelineRepo(string adoOrg, string teamProject, int pipelineId, string defaultBranch, string clean, string checkoutSubmodules, string githubOrg, string githubRepo, string connectedServiceId)
        {
            var url = $"{_adoBaseUrl}/{adoOrg}/{teamProject}/_apis/build/definitions/{pipelineId}?api-version=6.0";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            var newRepo = new
            {
                properties = new
                {
                    apiUrl = $"https://api.github.com/repos/{githubOrg}/{githubRepo}",
                    branchesUrl = $"https://api.github.com/repos/{githubOrg}/{githubRepo}/branches",
                    cloneUrl = $"https://github.com/{githubOrg}/{githubRepo}.git",
                    connectedServiceId,
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
            };

            var payload = new JObject();

            foreach (var prop in data.Properties())
            {
                if (prop.Name == "repository")
                {
                    prop.Value = JObject.Parse(newRepo.ToJson());
                }

                payload.Add(prop.Name, prop.Value);
            }

            await _client.PutAsync(url, payload.ToObject(typeof(object)));
        }

        public virtual async Task<string> GetBoardsGithubRepoId(string org, string teamProject, string teamProjectId, string endpointId, string githubOrg, string githubRepo)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["dataProviders"]["ms.vss-work-web.github-user-repository-data-provider"]["additionalProperties"]["nodeId"];
        }

        public virtual async Task CreateBoardsGithubConnection(string org, string teamProject, string endpointId, string repoId)
        {
            var url = $"{_adoBaseUrl}/{org}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

            await _client.PostAsync(url, payload);
        }

        public virtual async Task DisableRepo(string org, string teamProject, string repoId)
        {
            var url = $"{_adoBaseUrl}/{org}/{teamProject}/_apis/git/repositories/{repoId}?api-version=6.1-preview.1";

            var payload = new { isDisabled = true };
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

            var url = $"{_adoBaseUrl}/{org}/_apis/accesscontrolentries/{gitReposNamespace}?api-version=6.1-preview.1";

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

            await _client.PostAsync(url, payload);
        }
    }
}
