using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

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

    // Basic HTTP wrapper methods for use by other services
    public virtual async Task<string> GetAsync(string relativeUrl)
    {
        ArgumentNullException.ThrowIfNull(relativeUrl);
        var url = relativeUrl.StartsWith("http") ? relativeUrl : $"{_adoBaseUrl}{relativeUrl}";
        return await _client.GetAsync(url);
    }

    public virtual async Task PutAsync(string relativeUrl, object payload)
    {
        ArgumentNullException.ThrowIfNull(relativeUrl);
        var url = relativeUrl.StartsWith("http") ? relativeUrl : $"{_adoBaseUrl}{relativeUrl}";
        await _client.PutAsync(url, payload);
    }

    public virtual async Task<string> PostAsync(string relativeUrl, object payload)
    {
        ArgumentNullException.ThrowIfNull(relativeUrl);
        var url = relativeUrl.StartsWith("http") ? relativeUrl : $"{_adoBaseUrl}{relativeUrl}";
        return await _client.PostAsync(url, payload);
    }

    public virtual async Task<string> GetOrgOwner(string org)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repo.EscapeDataString()}/pushes?$top=1&api-version=7.1-preview.2";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);
        var pushDate = data.TryGetValue("value", out var dataValue) && dataValue.Any() ? (DateTime)dataValue.First()["date"] : DateTime.MinValue;

        return pushDate.Date;
    }

    public virtual async Task<int> GetCommitCountSince(string org, string teamProject, string repo, DateTime fromDate)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repo.EscapeDataString()}/commits?searchCriteria.fromDate={fromDate.ToString("d", CultureInfo.InvariantCulture)}&api-version=7.1-preview.1";
        return await _client.GetCountUsingSkip(url);
    }

    public virtual async Task<IEnumerable<string>> GetPushersSince(string org, string teamProject, string repo, DateTime fromDate)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repo.EscapeDataString()}/pushes?searchCriteria.fromDate={fromDate.ToString("d", CultureInfo.InvariantCulture)}&api-version=7.1-preview.1";
        var response = await _client.GetWithPagingTopSkipAsync(url, x => $"{x["pushedBy"]["displayName"]} ({x["pushedBy"]["uniqueName"]})");

        return response;
    }

    public virtual async Task<int> GetPullRequestCount(string org, string teamProject, string repo)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repo.EscapeDataString()}/pullrequests?searchCriteria.status=all&api-version=7.1-preview.1";
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
        var url = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId.EscapeDataString()}?api-version=5.0-preview.1";
        var response = await _client.GetAsync(url);
        return JArray.Parse(response)
                     .Children()
                     .Select(x => (string)x["AccountName"])
                     .ToList();
    }

    public virtual async Task<string> GetOrganizationId(string userId, string adoOrganization)
    {
        var url = $"https://app.vssps.visualstudio.com/_apis/accounts?memberId={userId.EscapeDataString()}&api-version=5.0-preview.1";

        var data = await _client.GetWithPagingAsync(url);

        // TODO: This will crash if this org doesn't exist, or the PAT doesn't have access to it
        return (string)data.Children().Single(x => ((string)x["accountName"]).ToUpper() == adoOrganization.ToUpper())["accountId"];
    }

    public virtual async Task<IEnumerable<string>> GetTeamProjects(string org)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/projects?api-version=6.1-preview";
        var data = await _client.GetWithPagingAsync(url);
        return data.Select(x => (string)x["name"]).ToList();
    }

    public virtual async Task<IEnumerable<AdoRepository>> GetEnabledRepos(string org, string teamProject) => (await GetRepos(org, teamProject)).Where(x => !x.IsDisabled).ToList();

    public virtual async Task<IEnumerable<AdoRepository>> GetRepos(string org, string teamProject)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories?api-version=6.1-preview.1";
        var data = await _client.GetWithPagingAsync(url);
        return data
            .Select(x => new AdoRepository
            {
                Id = (string)x["id"],
                Name = (string)x["name"],
                Size = ((string)x["size"]).ToULongOrNull(),
                IsDisabled = ((string)x["isDisabled"]).ToBool()
            })
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
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=6.0-preview.4";
        var response = await _client.GetWithPagingAsync(url);

        var endpoint = response.FirstOrDefault(x =>
            (((string)x["type"]).Equals("GitHub", StringComparison.OrdinalIgnoreCase) &&
             ((string)x["name"]).Equals(githubOrg, StringComparison.OrdinalIgnoreCase)) ||
            (((string)x["type"]).Equals("GitHubProximaPipelines", StringComparison.OrdinalIgnoreCase) &&
             ((string)x["name"]).Equals(teamProject, StringComparison.OrdinalIgnoreCase)));

        return endpoint != null ? (string)endpoint["id"] : null;
    }

    public virtual async Task<string> GetGithubHandle(string org, string teamProject, string githubToken)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

        // Check for error message in the response
        var errorMessage = ExtractErrorMessage(response, "ms.vss-work-web.github-user-data-provider");
        if (errorMessage.HasValue())
        {
            throw new OctoshiftCliException($"Error validating GitHub token: {errorMessage}");
        }

        var data = JObject.Parse(response);
        var dataProviders = data["dataProviders"] ?? throw new OctoshiftCliException("Missing data from 'ms.vss-work-web.github-user-data-provider'. Please ensure the Azure DevOps project has a configured GitHub connection.");
        var dataProvider = dataProviders["ms.vss-work-web.github-user-data-provider"] ?? throw new OctoshiftCliException("Missing data from 'ms.vss-work-web.github-user-data-provider'. Please ensure the Azure DevOps project has a configured GitHub connection.");

        return (string)dataProvider["login"];
    }

    public virtual async Task<(string connectionId, string endpointId, string connectionName, IEnumerable<string> repoIds)> GetBoardsGithubConnection(string org, string teamProject)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProjectId.EscapeDataString()}/_apis/serviceendpoint/endpoints?api-version=5.0-preview.1";

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
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

        var response = await _client.PostAsync(url, payload);

        // Check for error message in the response
        var errorMessage = ExtractErrorMessage(response, "ms.vss-work-web.azure-boards-save-external-connection-data-provider");
        if (errorMessage.HasValue())
        {
            throw new OctoshiftCliException($"Error adding repository to boards GitHub connection: {errorMessage}");
        }
    }

    public virtual async Task<string> GetTeamProjectId(string org, string teamProject)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/projects/{teamProject.EscapeDataString()}?api-version=5.0-preview.1";
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
            var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repo.EscapeDataString()}?api-version=4.1";

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

        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories?api-version=4.1";

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
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions?repositoryId={repoId.EscapeDataString()}&repositoryType=TfsGit&queryOrder=lastModifiedDescending";
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

        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions?queryOrder=definitionNameAscending";
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

        return _pipelineIds.TryGetValue((org.ToUpper(), teamProject.ToUpper(), pipelinePath.ToUpper()), out result)
            ? result
            : response.Count(x => ((string)x["name"]).ToUpper() == pipeline.ToUpper()) == 1
            ? (int)response.Single(x => ((string)x["name"]).ToUpper() == pipeline.ToUpper())["id"]
            : throw new ArgumentException("Unable to find the specified pipeline", nameof(pipeline));
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
        var url = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/{adoTeamProject.EscapeDataString()}/_apis/serviceendpoint/endpoints/{serviceConnectionId.EscapeDataString()}?api-version=6.0-preview.4";

        var response = await _client.GetAsync(url);

        // When the service connection isn't shared with this team project, the response is always 'null'
        return response.HasValue() && !response.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    public virtual async Task ShareServiceConnection(string adoOrg, string adoTeamProject, string adoTeamProjectId, string serviceConnectionId)
    {
        var url = $"{_adoBaseUrl}/{adoOrg.EscapeDataString()}/_apis/serviceendpoint/endpoints/{serviceConnectionId.EscapeDataString()}?api-version=6.0-preview.4";

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

    public virtual async Task<(string DefaultBranch, string Clean, string CheckoutSubmodules, JToken Triggers)> GetPipeline(string org, string teamProject, int pipelineId)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

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

        // Capture trigger information to preserve during rewiring
        var triggers = data["triggers"];

        return (defaultBranch, clean, checkoutSubmodules, triggers);
    }

    public virtual async Task<string> GetBoardsGithubRepoId(string org, string teamProject, string teamProjectId, string endpointId, string githubOrg, string githubRepo)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

        // Check for error message in the response
        var errorMessage = ExtractErrorMessage(response, "ms.vss-work-web.github-user-repository-data-provider");
        if (errorMessage.HasValue())
        {
            throw new OctoshiftCliException($"Error getting GitHub repository information: {errorMessage}");
        }

        var data = JObject.Parse(response);
        var dataProviders = data["dataProviders"] ?? throw new OctoshiftCliException("Could not retrieve GitHub repository information. Please verify the repository exists and the GitHub token has the correct permissions.");
        var dataProvider = dataProviders["ms.vss-work-web.github-user-repository-data-provider"];

#pragma warning disable IDE0046 // Convert to conditional expression
        if (dataProvider == null || dataProvider["additionalProperties"] == null || dataProvider["additionalProperties"]["nodeId"] == null)
#pragma warning restore IDE0046 // Convert to conditional expression
        {
            throw new OctoshiftCliException("Could not retrieve GitHub repository information. Please verify the repository exists and the GitHub token has the correct permissions.");
        }

        return (string)dataProvider["additionalProperties"]["nodeId"];
    }

    public virtual async Task CreateBoardsGithubConnection(string org, string teamProject, string endpointId, string repoId)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/Contribution/HierarchyQuery?api-version=5.0-preview.1";

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

        var response = await _client.PostAsync(url, payload);

        // Check for error message in the response
        var errorMessage = ExtractErrorMessage(response, "ms.vss-work-web.azure-boards-save-external-connection-data-provider");
        if (errorMessage.HasValue())
        {
            throw new OctoshiftCliException($"Error creating boards GitHub connection: {errorMessage}");
        }
    }

    public virtual async Task DisableRepo(string org, string teamProject, string repoId)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/git/repositories/{repoId.EscapeDataString()}?api-version=6.1-preview.1";

        var payload = new { isDisabled = true };
        await _client.PatchAsync(url, payload);
    }

    public virtual async Task<string> GetIdentityDescriptor(string org, string teamProjectId, string groupName)
    {
        var url = $"https://vssps.dev.azure.com/{org.EscapeDataString()}/_apis/identities?searchFilter=General&filterValue={groupName.EscapeDataString()}&queryMembership=None&api-version=6.1-preview.1";

        var identities = await _client.GetWithPagingAsync(url);

        var result = identities.Single(x => ((string)x["properties"]["LocalScopeId"]["$value"]) == teamProjectId);
        return (string)result["descriptor"];
    }

    public virtual async Task LockRepo(string org, string teamProjectId, string repoId, string identityDescriptor)
    {
        var gitReposNamespace = "2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87";

        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/accesscontrolentries/{gitReposNamespace.EscapeDataString()}?api-version=6.1-preview.1";

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

    public virtual async Task<bool> IsCallerOrgAdmin(string org)
    {
        const string collectionSecurityNamespaceId = "3e65f728-f8bc-4ecd-8764-7e378b19bfa7";
        const int genericWritePermissionBitMaskValue = 2;
        return await HasPermission(org, collectionSecurityNamespaceId, genericWritePermissionBitMaskValue);
    }

    private string ExtractErrorMessage(string response, string dataProviderKey)
    {
        if (!response.HasValue())
        {
            return null;
        }

        var data = JObject.Parse(response);
#pragma warning disable IDE0046 // Convert to conditional expression
        if (data["dataProviders"] is not JObject dataProviders)
        {
            return null;
        }
#pragma warning restore IDE0046 // Convert to conditional expression

        return dataProviders[dataProviderKey] is not JObject dataProvider ? null : (string)dataProvider["errorMessage"];
    }

    private async Task<bool> HasPermission(string org, string securityNamespaceId, int permission)
    {
        var response = await _client.GetAsync($"{_adoBaseUrl}/{org.EscapeDataString()}/_apis/permissions/{securityNamespaceId.EscapeDataString()}/{permission}?api-version=6.0");
        return ((string)JObject.Parse(response)["value"]?.FirstOrDefault()).ToBool();
    }

    public virtual async Task<int> QueueBuild(string org, string teamProject, int pipelineId, string sourceBranch = "refs/heads/main")
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/builds?api-version=6.0";

        var payload = new
        {
            definition = new
            {
                id = pipelineId
            },
            sourceBranch,
            reason = "manual"
        };

        var response = await _client.PostAsync(url, payload);
        var data = JObject.Parse(response);
        return (int)data["id"];
    }

    public virtual async Task<(string status, string result, string url)> GetBuildStatus(string org, string teamProject, int buildId)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/builds/{buildId}?api-version=6.0";

        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        var status = (string)data["status"];
        var result = (string)data["result"];
        var buildUrl = (string)data["_links"]["web"]["href"];

        return (status, result, buildUrl);
    }

    public virtual async Task<IEnumerable<(int buildId, string status, string result, string url, DateTime queueTime)>> GetBuilds(string org, string teamProject, int pipelineId, DateTime? minTime = null)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/builds?definitions={pipelineId}&api-version=6.0";

        if (minTime.HasValue)
        {
            url += $"&minTime={minTime.Value:yyyy-MM-ddTHH:mm:ss.fffZ}";
        }

        var response = await _client.GetWithPagingAsync(url);

        return response.Select(build => (
            buildId: (int)build["id"],
            status: (string)build["status"],
            result: (string)build["result"],
            url: (string)build["_links"]["web"]["href"],
            queueTime: (DateTime)build["queueTime"]
        )).ToList();
    }

    public virtual async Task<(string repoName, string repoId, string defaultBranch, string clean, string checkoutSubmodules)> GetPipelineRepository(string org, string teamProject, int pipelineId)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        var repository = data["repository"];
        var repoName = (string)repository["name"];
        var repoId = (string)repository["id"];
        var defaultBranch = (string)repository["defaultBranch"];
        var clean = (string)repository["clean"];
        var checkoutSubmodules = (string)repository["checkoutSubmodules"];

        if (defaultBranch.ToLower().StartsWith("refs/heads/"))
        {
            defaultBranch = defaultBranch["refs/heads/".Length..];
        }

        clean = clean == null ? "null" : clean.ToLower();
        checkoutSubmodules = checkoutSubmodules == null ? "null" : checkoutSubmodules.ToLower();

        return (repoName, repoId, defaultBranch, clean, checkoutSubmodules);
    }

    public virtual async Task RestorePipelineToAdoRepo(string org, string teamProject, int pipelineId, string adoRepoName, string defaultBranch, string clean, string checkoutSubmodules, JToken originalTriggers)
    {
        var url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_apis/build/definitions/{pipelineId}?api-version=6.0";

        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        // Get the ADO repository ID
        var adoRepoId = await GetRepoId(org, teamProject, adoRepoName);

        // Create ADO repository configuration
        var adoRepo = new
        {
            id = adoRepoId,
            type = "TfsGit",
            name = adoRepoName,
            url = $"{_adoBaseUrl}/{org.EscapeDataString()}/{teamProject.EscapeDataString()}/_git/{adoRepoName.EscapeDataString()}",
            defaultBranch,
            clean,
            checkoutSubmodules,
            properties = new
            {
                cleanOptions = "0",
                labelSources = "0",
                labelSourcesFormat = "$(build.buildNumber)",
                reportBuildStatus = "true",
                gitLfsSupport = "false",
                skipSyncSource = "false",
                checkoutNestedSubmodules = "false",
                fetchDepth = "0"
            }
        };

        var payload = new JObject();

        foreach (var prop in data.Properties())
        {
            if (prop.Name == "repository")
            {
                prop.Value = JObject.Parse(adoRepo.ToJson());
            }
            else if (prop.Name == "triggers")
            {
                prop.Value = originalTriggers;
            }

            payload.Add(prop.Name, prop.Value);
        }

        // Add triggers if no triggers property exists
        payload["triggers"] ??= originalTriggers;

        // Restore to UI-controlled settings for ADO repos
        payload["settingsSourceType"] = 1;

        await _client.PutAsync(url, payload.ToObject(typeof(object)));
    }
}
