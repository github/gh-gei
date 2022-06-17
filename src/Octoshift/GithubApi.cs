using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Models;

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

        public virtual async Task<bool> RepoExists(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}";

            try
            {
                await _client.GetAsync(url);
                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public virtual async Task RemoveTeamMember(string org, string teamSlug, string member)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamSlug}/memberships/{member}";

            await _client.DeleteAsync(url);
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
                // TODO: this is super ugly, need to find a graphql library to make this code nicer
                query = "query($login: String!) {organization(login: $login) { login, id, name } }",
                variables = new { login = org }
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["organization"]["id"];
        }

        public virtual async Task<string> GetRepositoryId(string org, string repo)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                // TODO: this is super ugly, need to find a graphql library to make this code nicer
                query = "query repository($owner: String!, $name: String!) { repository(name: $name, owner: $owner) { id } }",
                variables = new { owner = org, name = repo }
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            CheckForErrors(data);

            return (string)data["data"]!["repository"]!["id"];
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

        public virtual async Task<string> StartMigration(string migrationSourceId, string sourceRepoUrl, string orgId, string repo, string sourceToken, string targetToken, string gitArchiveUrl = null, string metadataArchiveUrl = null, bool skipReleases = false)
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
                    $skipReleases: Boolean)";
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
                        skipReleases: $skipReleases
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
                    skipReleases
                },
                operationName = "startRepositoryMigration"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["startRepositoryMigration"]["repositoryMigration"]["id"];
        }

        public virtual async Task<(string State, string RepositoryName, string FailureReason)> GetMigration(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason, repositoryName } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            var response = await _retryPolicy.HttpRetry(async () => await _client.PostAsync(url, payload),
                ex => ex.StatusCode == HttpStatusCode.BadGateway);
            var data = JObject.Parse(response);

            return (
                State: (string)data["data"]["node"]["state"],
                RepositoryName: (string)data["data"]["node"]["repositoryName"],
                FailureReason: (string)data["data"]["node"]["failureReason"]);
        }

        public virtual async Task<IEnumerable<(string MigrationId, string State)>> GetMigrationStates(string orgId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!, $first: Int, $after: String)";
            var gql = @" 
                node(id: $id) { 
                    ... on Organization { 
                        login, 
                        repositoryMigrations(first: $first, after: $after) {
                            pageInfo {
                                endCursor
                                hasNextPage
                            }
                            totalCount
                            nodes {
                                id
                                sourceUrl
                                migrationSource { name }
                                state
                                failureReason
                                createdAt
                            }
                        }
                    }
                }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new { id = orgId }
            };

            return await _client.PostGraphQLWithPaginationAsync(
                    url,
                    payload,
                    obj => (JArray)obj["data"]["node"]["repositoryMigrations"]["nodes"],
                    obj => (JObject)obj["data"]["node"]["repositoryMigrations"]["pageInfo"],
                    5)
                .Select(jToken => ((string)jToken["id"], (string)jToken["state"]))
                .ToListAsync();
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
            var response = await _client.PostAsync(url, payload);

            var data = JObject.Parse(response);
            var nodes = (JArray)data["data"]["organization"]["repositoryMigrations"]["nodes"];

            return nodes.Count == 0 ? null : (string)nodes[0]["migrationLogUrl"];
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

            await _client.PatchAsync(url, payload);
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

        public virtual async Task<int> StartMetadataArchiveGeneration(string org, string repo)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations";

            var options = new
            {
                repositories = new[] { repo },
                exclude_git_data = true,
                exclude_releases = true,
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

            return await _client.PostGraphQLWithPaginationAsync(
                    url,
                    payload,
                    data => (JArray)data["data"]["node"]["mannequins"]["nodes"],
                    data => (JObject)data["data"]["node"]["mannequins"]["pageInfo"])
                .Select(mannequin => BuildMannequin(mannequin))
                .ToListAsync();
        }

        public virtual async Task<string> GetUserId(string login)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                query = "query($login: String!) {user(login: $login) { id, name } }",
                variables = new { login }
            };

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

        public virtual async Task ArchiveRepository(string sourceOrg, string sourceRepo)
        {
            var repositoryId = await GetRepositoryId(sourceOrg, sourceRepo);

            var url = $"{_apiUrl}/graphql";

            var query = "mutation archiveRepository($repoId: ID!)";
            var gql = "archiveRepository(input: {repositoryId: $repoId}) { clientMutationId }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    repoId = repositoryId
                },
                operationName = "archiveRepository"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            CheckForErrors(data);
        }

        public virtual async Task<bool> IsRepoArchived(string org, string repo)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                // TODO: this is super ugly, need to find a graphql library to make this code nicer
                query = "query repository($owner: String!, $name: String!) { repository(name: $name, owner: $owner) { isArchived } }",
                variables = new { owner = org, name = repo }
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            CheckForErrors(data);

            return (bool)data["data"]!["repository"]!["isArchived"];
        }

        protected void CheckForErrors(JObject responseJObject)
        {
            if (responseJObject == null || !responseJObject.ContainsKey("errors") || !responseJObject["errors"]!.HasValues)
            {
                return;
            }

            var errorMessageStringBuilder = new StringBuilder();

            foreach (var error in responseJObject["errors"])
            {
                var message = error["message"];

                if (message == null)
                {
                    continue;
                }

                errorMessageStringBuilder.AppendLine(message.ToString());
            }

            throw new OctoshiftCliException(errorMessageStringBuilder.ToString());
        }
    }
}
