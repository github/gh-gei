using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Models;
using Polly;

namespace OctoshiftCLI
{
    public class GithubApi
    {
        private readonly GithubClient _client;
        private readonly string _apiUrl;
        private readonly RetryPolicy _retryPolicy;

        public GithubApi(GithubClient client, string apiUrl, RetryPolicy retryPolicy)
        {
            _client = client;
            _apiUrl = apiUrl;
            _retryPolicy = retryPolicy;
        }

        public virtual async Task AddAutoLink(string org, string repo, string keyPrefix, string urlTemplate)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
            {
                throw new ArgumentException($"Invalid value for {nameof(keyPrefix)}");
            }
            if (string.IsNullOrWhiteSpace(urlTemplate))
            {
                throw new ArgumentException($"Invalid value for {nameof(urlTemplate)}");
            }

            var url = $"{_apiUrl}/repos/{org}/{repo}/autolinks";

            var payload = new
            {
                key_prefix = keyPrefix,
                url_template = urlTemplate.Replace(" ", "%20")
            };

            await _client.PostAsync(url, payload);
        }

        public virtual async Task<List<(int Id, string KeyPrefix, string UrlTemplate)>> GetAutoLinks(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/autolinks";

            return await _client.GetAllAsync(url)
                                .Select(al => ((int)al["id"], (string)al["key_prefix"], (string)al["url_template"]))
                                .ToListAsync();
        }

        public virtual async Task DeleteAutoLink(string org, string repo, int autoLinkId)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/autolinks/{autoLinkId}";

            await _client.DeleteAsync(url);
        }

        public virtual async Task<string> CreateTeam(string org, string teamName)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";
            var payload = new { name = teamName, privacy = "closed" };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public virtual async Task<IEnumerable<string>> GetTeams(string org)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";

            return await _client.GetAllAsync(url)
                .Select(t => (string)t["name"])
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<string>> GetTeamMembers(string org, string teamSlug)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamSlug}/members?per_page=100";

            return await _retryPolicy.HttpRetry(async () => await _client.GetAllAsync(url).Select(x => (string)x["login"]).ToListAsync(),
                                            ex => ex.StatusCode == HttpStatusCode.NotFound);
        }

        public virtual async Task<IEnumerable<string>> GetRepos(string org)
        {
            var url = $"{_apiUrl}/orgs/{org}/repos?per_page=100";

            return await _client.GetAllAsync(url).Select(x => (string)x["name"]).ToListAsync();
        }

        public virtual async Task RemoveTeamMember(string org, string teamSlug, string member)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamSlug}/memberships/{member}";

            await _retryPolicy.HttpRetry(() => _client.DeleteAsync(url), _ => true);
        }

        public virtual async Task AddTeamSync(string org, string teamName, string groupId, string groupName, string groupDesc)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamName}/team-sync/group-mappings";
            var payload = new
            {
                groups = new[]
                {
                    new { group_id = groupId, group_name = groupName, group_description = groupDesc }
                }
            };

            await _client.PatchAsync(url, payload);
        }

        public virtual async Task AddTeamToRepo(string org, string repo, string teamSlug, string role)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamSlug}/repos/{org}/{repo}";
            var payload = new { permission = role };

            await _client.PutAsync(url, payload);
        }

        public virtual async Task<string> GetOrganizationId(string org)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                query = "query($login: String!) {organization(login: $login) { login, id, name } }",
                variables = new { login = org }
            };

            var response = await _retryPolicy.Retry(async () =>
            {
                var httpResponse = await _client.PostAsync(url, payload);
                var data = JObject.Parse(httpResponse);

                EnsureSuccessGraphQLResponse(data);

                return (string)data["data"]["organization"]["id"];
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException("Failed to lookup the Organization ID", response.FinalException)
                : response.Result;
        }

        public virtual async Task<string> GetEnterpriseId(string enterpriseName)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                query = "query($slug: String!) {enterprise (slug: $slug) { slug, id } }",
                variables = new { slug = enterpriseName }
            };

            var response = await _retryPolicy.Retry(async () =>
            {
                var httpResponse = await _client.PostAsync(url, payload);
                var data = JObject.Parse(httpResponse);

                EnsureSuccessGraphQLResponse(data);

                return (string)data["data"]["enterprise"]["id"];
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException("Failed to lookup the Enterprise ID", response.FinalException)
                : response.Result;
        }

        public virtual async Task<string> CreateAdoMigrationSource(string orgId, string adoServerUrl)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } }";

            adoServerUrl = adoServerUrl.HasValue() ? adoServerUrl : "https://dev.azure.com";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    name = "Azure DevOps Source",
                    url = adoServerUrl,
                    ownerId = orgId,
                    type = "AZURE_DEVOPS"
                },
                operationName = "createMigrationSource"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public virtual async Task<string> CreateBbsMigrationSource(string orgId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    name = "Bitbucket Server Source",
                    url = "https://not-used",
                    ownerId = orgId,
                    type = "BITBUCKET_SERVER"
                },
                operationName = "createMigrationSource"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public virtual async Task<string> CreateGhecMigrationSource(string orgId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) { migrationSource { id, name, url, type } }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    name = "GHEC Source",
                    url = "https://github.com",
                    ownerId = orgId,
                    type = "GITHUB_ARCHIVE"
                },
                operationName = "createMigrationSource"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public virtual async Task<string> StartMigration(string migrationSourceId, string sourceRepoUrl, string orgId, string repo, string sourceToken, string targetToken, string gitArchiveUrl = null, string metadataArchiveUrl = null, bool skipReleases = false, bool lockSource = false)
        {
            var url = $"{_apiUrl}/graphql";

            var query = @"
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
                    $lockSource: Boolean)";
            var gql = @"
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
                        lockSource: $lockSource
                    }
                ) {
                    repositoryMigration {
                        id,
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
                    repositoryName = repo,
                    continueOnError = true,
                    gitArchiveUrl,
                    metadataArchiveUrl,
                    accessToken = sourceToken,
                    githubPat = targetToken,
                    skipReleases,
                    lockSource
                },
                operationName = "startRepositoryMigration"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            EnsureSuccessGraphQLResponse(data);

            return (string)data["data"]["startRepositoryMigration"]["repositoryMigration"]["id"];
        }

        public virtual async Task<string> StartOrganizationMigration(string sourceOrgUrl, string targetOrgName, string targetEnterpriseId, string sourceAccessToken)
        {
            var url = $"{_apiUrl}/graphql";

            var query = @"
                mutation startOrganizationMigration (
                        $sourceOrgUrl: URI!,
                        $targetOrgName: String!,
                        $targetEnterpriseId: ID!,
                        $sourceAccessToken: String!)";
            var gql = @"
                startOrganizationMigration( 
                    input: {
                        sourceOrgUrl: $sourceOrgUrl,
                        targetOrgName: $targetOrgName,
                        targetEnterpriseId: $targetEnterpriseId,
                        sourceAccessToken: $sourceAccessToken
                    }) {
                        orgMigration {
                            id
                        }
                    }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    sourceOrgUrl,
                    targetOrgName,
                    targetEnterpriseId,
                    sourceAccessToken
                },
                operationName = "startOrganizationMigration"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            EnsureSuccessGraphQLResponse(data);

            return (string)data["data"]["startOrganizationMigration"]["orgMigration"]["id"];
        }

        public virtual async Task<(string State, string SourceOrgUrl, string TargetOrgName, string FailureReason, int? RemainingRepositoriesCount, int? TotalRepositoriesCount)> GetOrganizationMigration(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            var response = await _retryPolicy.Retry(async () =>
            {
                var httpResponse = await _client.PostAsync(url, payload);
                var data = JObject.Parse(httpResponse);

                EnsureSuccessGraphQLResponse(data);

                return (
                    State: (string)data["data"]["node"]["state"],
                    SourceOrgUrl: (string)data["data"]["node"]["sourceOrgUrl"],
                    TargetOrgName: (string)data["data"]["node"]["targetOrgName"],
                    FailureReason: (string)data["data"]["node"]["failureReason"],
                    RemainingRepositoriesCount: (int?)data["data"]["node"]["remainingRepositoriesCount"],
                    TotalRepositoriesCount: (int?)data["data"]["node"]["totalRepositoriesCount"]);
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException($"Failed to get migration state for migration {migrationId}", response.FinalException)
                : response.Result;
        }

        public virtual async Task<string> StartBbsMigration(string migrationSourceId, string orgId, string repo, string targetToken, string archiveUrl)
        {
            return await StartMigration(
                migrationSourceId,
                "https://not-used",  // source repository URL
                orgId,
                repo,
                "not-used",  // source access token
                targetToken,
                archiveUrl,
                "https://not-used"  // metadata archive URL
            );
        }

        public virtual async Task<(string State, string RepositoryName, string FailureReason)> GetMigration(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason, repositoryName } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            var response = await _retryPolicy.Retry(async () =>
            {
                var httpResponse = await _client.PostAsync(url, payload);
                var data = JObject.Parse(httpResponse);

                EnsureSuccessGraphQLResponse(data);

                return (
                    State: (string)data["data"]["node"]["state"],
                    RepositoryName: (string)data["data"]["node"]["repositoryName"],
                    FailureReason: (string)data["data"]["node"]["failureReason"]);
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException($"Failed to get migration state for migration {migrationId}", response.FinalException)
                : response.Result;
        }

        public virtual async Task<string> GetMigrationLogUrl(string org, string repo)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query ($org: String!, $repo: String!)";
            var gql = @"
                organization(login: $org) {
                    repositoryMigrations(last: 1, repositoryName: $repo) {
                        nodes {
                            migrationLogUrl
                        }
                    }
                }
            ";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { org, repo } };

            var response = await _retryPolicy.Retry(async () =>
            {
                var httpResponse = await _client.PostAsync(url, payload);
                var data = JObject.Parse(httpResponse);

                EnsureSuccessGraphQLResponse(data);

                var nodes = (JArray)data["data"]["organization"]["repositoryMigrations"]["nodes"];

                return nodes.Count == 0 ? null : (string)nodes[0]["migrationLogUrl"];
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException($"Failed to get migration log URL.", response.FinalException)
                : response.Result;
        }

        public virtual async Task<int> GetIdpGroupId(string org, string groupName)
        {
            var url = $"{_apiUrl}/orgs/{org}/external-groups";

            // TODO: Need to implement paging
            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return (int)data["groups"].Children().Single(x => ((string)x["group_name"]).ToUpper() == groupName.ToUpper())["group_id"];
        }

        public virtual async Task<string> GetTeamSlug(string org, string teamName)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";

            var response = await _client.GetAllAsync(url)
                                        .SingleAsync(x => ((string)x["name"]).ToUpper() == teamName.ToUpper());

            return (string)response["slug"];
        }

        public virtual async Task AddEmuGroupToTeam(string org, string teamSlug, int groupId)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamSlug}/external-groups";
            var payload = new { group_id = groupId };

            await _retryPolicy.HttpRetry(async () => await _client.PatchAsync(url, payload),
                ex => ex.StatusCode == HttpStatusCode.BadRequest);
        }

        public virtual async Task<bool> GrantMigratorRole(string org, string actor, string actorType)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! )";
            var gql = "grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new { organizationId = org, actor, actor_type = actorType },
                operationName = "grantMigratorRole"
            };

            try
            {
                var response = await _client.PostAsync(url, payload);
                var data = JObject.Parse(response);

                return (bool)data["data"]["grantMigratorRole"]["success"];
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public virtual async Task<bool> RevokeMigratorRole(string org, string actor, string actorType)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! )";
            var gql = "revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new { organizationId = org, actor, actor_type = actorType },
                operationName = "revokeMigratorRole"
            };

            try
            {
                var response = await _client.PostAsync(url, payload);
                var data = JObject.Parse(response);

                return (bool)data["data"]["revokeMigratorRole"]["success"];
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public virtual async Task DeleteRepo(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}";
            await _client.DeleteAsync(url);
        }

        public virtual async Task<int> StartGitArchiveGeneration(string org, string repo)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations";

            var options = new
            {
                repositories = new[] { repo },
                exclude_metadata = true
            };

            var response = await _client.PostAsync(url, options);
            var data = JObject.Parse(response);
            return (int)data["id"];
        }

        public virtual async Task<int> StartMetadataArchiveGeneration(string org, string repo, bool skipReleases, bool lockSource)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations";

            var options = new
            {
                repositories = new[] { repo },
                exclude_git_data = true,
                exclude_releases = skipReleases,
                lock_repositories = lockSource,
                exclude_owner_projects = true
            };

            var response = await _client.PostAsync(url, options);
            var data = JObject.Parse(response);
            return (int)data["id"];
        }

        public virtual async Task<string> GetArchiveMigrationStatus(string org, int migrationId)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations/{migrationId}";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return (string)data["state"];
        }

        public virtual async Task<string> GetArchiveMigrationUrl(string org, int migrationId)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations/{migrationId}/archive";

            var response = await _client.GetNonSuccessAsync(url, HttpStatusCode.Found);
            return response;
        }

        public virtual async Task<IEnumerable<Mannequin>> GetMannequins(string orgId)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = GetMannequinsPayload(orgId);

            var response = await _retryPolicy.Retry(async () =>
            {
                return await _client.PostGraphQLWithPaginationAsync(
                    url,
                    payload,
                    data => (JArray)data["data"]["node"]["mannequins"]["nodes"],
                    data => (JObject)data["data"]["node"]["mannequins"]["pageInfo"])
                .Select(mannequin => BuildMannequin(mannequin))
                .ToListAsync();
            });

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException($"Failed to retrieve the list of mannequins", response.FinalException)
                : (IEnumerable<Mannequin>)response.Result;
        }

        public virtual async Task<string> GetUserId(string login)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                query = "query($login: String!) {user(login: $login) { id, name } }",
                variables = new { login }
            };

            // TODO: Add retry logic here, but need to inspect the actual error message and differentiate between transient failure vs user doesn't exist (only retry on failure)
            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return data["data"]["user"].Any() ? (string)data["data"]["user"]["id"] : null;
        }

        public virtual async Task<MannequinReclaimResult> ReclaimMannequin(string orgId, string mannequinId, string targetUserId)
        {
            var url = $"{_apiUrl}/graphql";
            var mutation = "mutation($orgId: ID!,$sourceId: ID!,$targetId: ID!)";
            var gql = @"
	            createAttributionInvitation(
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
	            }";

            var payload = new
            {
                query = $"{mutation} {{ {gql} }}",
                variables = new { orgId, sourceId = mannequinId, targetId = targetUserId }
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return data.ToObject<MannequinReclaimResult>();
        }

        public virtual async Task<IEnumerable<GithubSecretScanningAlert>> GetSecretScanningAlertsForRepository(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/secret-scanning/alerts?per_page=100";
            return await _client.GetAllAsync(url)
                .Select(secretAlert => BuildSecretScanningAlert(secretAlert))
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<GithubSecretScanningAlertLocation>> GetSecretScanningAlertsLocations(string org, string repo, int alertNumber)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";
            return await _client.GetAllAsync(url)
                .Select(alertLocation => BuildSecretScanningAlertLocation(alertLocation))
                .ToListAsync();
        }

        public virtual async Task UpdateSecretScanningAlert(string org, string repo, int alertNumber, string state, string resolution = null)
        {
            if (!SecretScanningAlert.IsOpenOrResolved(state))
            {
                throw new ArgumentException($"Invalid value for {nameof(state)}");
            }

            if (SecretScanningAlert.IsResolved(state) && !SecretScanningAlert.IsValidDismissedReason(resolution))
            {
                throw new ArgumentException($"Invalid value for {nameof(resolution)}");
            }

            var url = $"{_apiUrl}/repos/{org}/{repo}/secret-scanning/alerts/{alertNumber}";

            object payload = state == SecretScanningAlert.AlertStateOpen ? new { state } : new { state, resolution };
            await _client.PatchAsync(url, payload);
        }

        private static object GetMannequinsPayload(string orgId)
        {
            var query = "query($id: ID!, $first: Int, $after: String)";
            var gql = @"
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
                }";

            return new
            {
                query = $"{query} {{ {gql} }}",
                variables = new { id = orgId }
            };
        }

        private static Mannequin BuildMannequin(JToken mannequin)
        {
            return new Mannequin
            {
                Id = (string)mannequin["id"],
                Login = (string)mannequin["login"],
                MappedUser = mannequin["claimant"].Any()
                                    ? new Claimant
                                    {
                                        Id = (string)mannequin["claimant"]["id"],
                                        Login = (string)mannequin["claimant"]["login"]
                                    }
                                    : null
            };
        }

        private void EnsureSuccessGraphQLResponse(JObject response)
        {
            if (response.TryGetValue("errors", out var jErrors) && jErrors is JArray { Count: > 0 } errors)
            {
                var error = (JObject)errors[0];
                var errorMessage = error.TryGetValue("message", out var jMessage) ? (string)jMessage : null;
                throw new OctoshiftCliException($"{errorMessage ?? "UNKNOWN"}");
            }
        }

        private static GithubSecretScanningAlert BuildSecretScanningAlert(JToken secretAlert) =>
            new()
            {
                Number = (int)secretAlert["number"],
                State = (string)secretAlert["state"],
                Resolution = (string)secretAlert["resolution"],
                SecretType = (string)secretAlert["secret_type"],
                Secret = (string)secretAlert["secret"],
            };

        private static GithubSecretScanningAlertLocation BuildSecretScanningAlertLocation(JToken alertLocation) =>
            new()
            {
                Path = (string)alertLocation["details"]["path"],
                StartLine = (int)alertLocation["details"]["start_line"],
                EndLine = (int)alertLocation["details"]["end_line"],
                StartColumn = (int)alertLocation["details"]["start_column"],
                EndColumn = (int)alertLocation["details"]["end_column"],
                BlobSha = (string)alertLocation["details"]["blob_sha"],
            };
    }
}
