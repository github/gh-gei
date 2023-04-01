using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octoshift;
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

        private const string INSUFFICIENT_PERMISSIONS_HELP_MESSAGE = ". Please check that (a) you are an organization owner or you have been granted the migrator role and (b) your personal access token has the correct scopes. For more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.";

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

        public virtual async Task<(string Id, string Slug)> CreateTeam(string org, string teamName)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";
            var payload = new { name = teamName, privacy = "closed" };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return ((string)data["id"], (string)data["slug"]);
        }

        public virtual async Task<IEnumerable<(string Name, string Slug)>> GetTeams(string org)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";

            return await _client.GetAllAsync(url)
                .Select(t => ((string)t["name"], (string)t["slug"]))
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

            await _retryPolicy.Retry(() => _client.DeleteAsync(url));
        }

        public virtual async Task<bool> DoesRepoExist(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}";
            try
            {
                await _client.GetNonSuccessAsync(url, HttpStatusCode.NotFound);
                return false;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
        }

        public virtual async Task<bool> DoesOrgExist(string org)
        {
            var url = $"{_apiUrl}/orgs/{org}";
            try
            {
                await _client.GetNonSuccessAsync(url, HttpStatusCode.NotFound);
                return false;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
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

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    var data = await _client.PostGraphQLAsync(url, payload);

                    return (string)data["data"]["organization"]["id"];
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to lookup the Organization ID for organization '{org}'", ex);
            }
        }

        public virtual async Task<string> GetEnterpriseId(string enterpriseName)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = new
            {
                query = "query($slug: String!) {enterprise (slug: $slug) { slug, id } }",
                variables = new { slug = enterpriseName }
            };

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    var data = await _client.PostGraphQLAsync(url, payload);

                    return (string)data["data"]["enterprise"]["id"];
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to lookup the Enterprise ID for enterprise '{enterpriseName}'", ex);
            }
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

            try
            {
                var data = await _client.PostGraphQLAsync(url, payload);
                return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
            }
            catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
            {
                throw new OctoshiftCliException(ex.Message + INSUFFICIENT_PERMISSIONS_HELP_MESSAGE, ex);
            }
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

            try
            {
                var data = await _client.PostGraphQLAsync(url, payload);
                return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
            }
            catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
            {
                throw new OctoshiftCliException(ex.Message + INSUFFICIENT_PERMISSIONS_HELP_MESSAGE, ex);
            }
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

            try
            {
                var data = await _client.PostGraphQLAsync(url, payload);
                return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
            }
            catch (OctoshiftCliException ex) when (ex.Message.Contains("not have the correct permissions to execute"))
            {
                throw new OctoshiftCliException(ex.Message + INSUFFICIENT_PERMISSIONS_HELP_MESSAGE, ex);
            }
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

            var data = await _client.PostGraphQLAsync(url, payload);

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

            var data = await _client.PostGraphQLAsync(url, payload);

            return (string)data["data"]["startOrganizationMigration"]["orgMigration"]["id"];
        }

        public virtual async Task<(string State, string SourceOrgUrl, string TargetOrgName, string FailureReason, int? RemainingRepositoriesCount, int? TotalRepositoriesCount)> GetOrganizationMigration(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on OrganizationMigration { state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    var data = await _client.PostGraphQLAsync(url, payload);

                    return (
                        State: (string)data["data"]["node"]["state"],
                        SourceOrgUrl: (string)data["data"]["node"]["sourceOrgUrl"],
                        TargetOrgName: (string)data["data"]["node"]["targetOrgName"],
                        FailureReason: (string)data["data"]["node"]["failureReason"],
                        RemainingRepositoriesCount: (int?)data["data"]["node"]["remainingRepositoriesCount"],
                        TotalRepositoriesCount: (int?)data["data"]["node"]["totalRepositoriesCount"]);
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to get migration state for migration {migrationId}", ex);
            }
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

        public virtual async Task<(string State, string RepositoryName, string FailureReason, string MigrationLogUrl)> GetMigration(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationLogUrl, migrationSource { name }, state, failureReason, repositoryName } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    var data = await _client.PostGraphQLAsync(url, payload);

                    return (
                        State: (string)data["data"]["node"]["state"],
                        RepositoryName: (string)data["data"]["node"]["repositoryName"],
                        FailureReason: (string)data["data"]["node"]["failureReason"],
                        MigrationLogUrl: (string)data["data"]["node"]["migrationLogUrl"]);
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to get migration state for migration {migrationId}", ex);
            }
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

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    var data = await _client.PostGraphQLAsync(url, payload);

                    var nodes = (JArray)data["data"]["organization"]["repositoryMigrations"]["nodes"];

                    return nodes.Count == 0 ? null : (string)nodes[0]["migrationLogUrl"];
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to get migration log URL.", ex);
            }
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
                var data = await _client.PostGraphQLAsync(url, payload);

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
                var data = await _client.PostGraphQLAsync(url, payload);

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

            try
            {
                var response = await _client.PostAsync(url, options);
                var data = JObject.Parse(response);
                return (int)data["id"];
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("configure blob storage"))
            {
                throw new OctoshiftCliException(ex.Message, ex);
            }
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

        public virtual async Task<string> GetArchiveMigrationStatus(string org, int archiveId)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations/{archiveId}";

            var response = await _retryPolicy.RetryOnResult(async () =>
            {
                var httpResponse = await _client.GetAsync(url);
                var data = JObject.Parse(httpResponse);

                return (string)data["state"];
            }, ArchiveMigrationStatus.Failed);

            return response.Outcome == OutcomeType.Failure
                ? throw new OctoshiftCliException($"Archive generation failed for id: {archiveId}")
                : response.Result;
        }

        public virtual async Task<string> GetArchiveMigrationUrl(string org, int archiveId)
        {
            var url = $"{_apiUrl}/orgs/{org}/migrations/{archiveId}/archive";

            var response = await _client.GetNonSuccessAsync(url, HttpStatusCode.Found);
            return response;
        }

        public virtual async Task<IEnumerable<Mannequin>> GetMannequins(string orgId)
        {
            var url = $"{_apiUrl}/graphql";

            var payload = GetMannequinsPayload(orgId);

            try
            {
                return await _retryPolicy.Retry(async () =>
                {
                    return await _client.PostGraphQLWithPaginationAsync(
                        url,
                        payload,
                        data => (JArray)data["data"]["node"]["mannequins"]["nodes"],
                        data => (JObject)data["data"]["node"]["mannequins"]["pageInfo"])
                    .Select(mannequin => BuildMannequin(mannequin))
                    .ToListAsync();
                });
            }
            catch (Exception ex)
            {
                throw new OctoshiftCliException($"Failed to retrieve the list of mannequins", ex);
            }
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
            var data = await _client.PostGraphQLAsync(url, payload);

            return (string)data["data"]["user"]["id"];
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

        public virtual async Task<IEnumerable<CodeScanningAnalysis>> GetCodeScanningAnalysisForRepository(string org, string repo, string branch = null)
        {
            var queryString = "per_page=100&sort=created&direction=asc";
            if (branch.HasValue())
            {
                queryString += $"&ref={branch}";
            }

            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/analyses?{queryString}";

            try
            {
                return await _client.GetAllAsync(url)
                    .Select(BuildCodeScanningAnalysis)
                    .ToListAsync();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.Message.Contains("no analysis found"))
            {
                return Enumerable.Empty<CodeScanningAnalysis>();
            }
        }

        public virtual async Task UpdateCodeScanningAlert(string org, string repo, int alertNumber, string state, string dismissedReason = null, string dismissedComment = null)
        {
            if (!CodeScanningAlertState.IsOpenOrDismissed(state))
            {
                throw new ArgumentException($"Invalid value for {nameof(state)}");
            }

            if (CodeScanningAlertState.IsDismissed(state) && !CodeScanningAlertState.IsValidDismissedReason(dismissedReason))
            {
                throw new ArgumentException($"Invalid value for {nameof(dismissedReason)}");
            }

            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/alerts/{alertNumber}";

            var payload = state == "open"
                ? (new { state })
                : (object)(new
                {
                    state,
                    dismissed_reason = dismissedReason,
                    dismissed_comment = dismissedComment ?? string.Empty
                });
            await _client.PatchAsync(url, payload);
        }

        public virtual async Task<string> GetSarifReport(string org, string repo, int analysisId)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/analyses/{analysisId}";
            // Need change the Accept header to application/sarif+json otherwise it will just be the analysis record
            var headers = new Dictionary<string, string>() { { "accept", "application/sarif+json" } };
            return await _client.GetAsync(url, headers);
        }

        public virtual async Task<string> UploadSarifReport(string org, string repo, string sarifReport, string commitSha, string sarifRef)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/sarifs";
            var payload = new
            {
                commit_sha = commitSha,
                sarif = StringCompressor.GZipAndBase64String(sarifReport),
                @ref = sarifRef
            };

            var response = await _retryPolicy.HttpRetry(async () => await _client.PostAsync(url, payload),
                                                        ex => ex.StatusCode == HttpStatusCode.BadGateway);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public virtual async Task<SarifProcessingStatus> GetSarifProcessingStatus(string org, string repo, string sarifId)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/sarifs/{sarifId}";
            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            var errors = data["errors"]?.ToObject<string[]>() ?? Array.Empty<string>();
            return new() { Status = (string)data["processing_status"], Errors = errors };
        }

        public virtual async Task<string> GetDefaultBranch(string org, string repo)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return (string)data["default_branch"];
        }

        public virtual async Task<IEnumerable<CodeScanningAlert>> GetCodeScanningAlertsForRepository(string org, string repo, string branch = null)
        {
            var queryString = "per_page=100&sort=created&direction=asc";
            if (branch.HasValue())
            {
                queryString += $"&ref={branch}";
            }
            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/alerts?{queryString}";
            return await _client.GetAllAsync(url)
                .Select(BuildCodeScanningAlert)
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<CodeScanningAlertInstance>> GetCodeScanningAlertInstances(string org, string repo, int alertNumber)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/code-scanning/alerts/{alertNumber}/instances?per_page=100";
            return await _client.GetAllAsync(url)
                .Select(BuildCodeScanningAlertInstance)
                .ToListAsync();
        }

        public virtual async Task<string> GetEnterpriseServerVersion()
        {
            var url = $"{_apiUrl}/meta";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return (string)data["installed_version"];
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

        private static CodeScanningAnalysis BuildCodeScanningAnalysis(JToken codescan) =>
            new()
            {
                Id = (int)codescan["id"],
                CommitSha = (string)codescan["commit_sha"],
                Ref = (string)codescan["ref"],
                CreatedAt = (string)codescan["created_at"],
            };

        private static CodeScanningAlert BuildCodeScanningAlert(JToken scanningAlert) =>

            new()
            {
                Number = (int)scanningAlert["number"],
                Url = (string)scanningAlert["url"],
                DismissedAt = scanningAlert.Value<string>("dismissed_at"),
                DismissedComment = scanningAlert.Value<string>("dismissed_comment"),
                DismissedReason = scanningAlert.Value<string>("dismissed_reason"),
                State = (string)scanningAlert["state"],
                RuleId = (string)scanningAlert["rule"]["id"],
                MostRecentInstance = BuildCodeScanningAlertInstance(scanningAlert["most_recent_instance"]),
            };

        private static CodeScanningAlertInstance BuildCodeScanningAlertInstance(JToken scanningAlertInstance) =>
            new()
            {
                Ref = (string)scanningAlertInstance["ref"],
                CommitSha = (string)scanningAlertInstance["commit_sha"],
                Path = (string)scanningAlertInstance["location"]["path"],
                StartLine = (int)scanningAlertInstance["location"]["start_line"],
                EndLine = (int)scanningAlertInstance["location"]["end_line"],
                StartColumn = (int)scanningAlertInstance["location"]["start_column"],
                EndColumn = (int)scanningAlertInstance["location"]["end_column"]
            };
    }
}
