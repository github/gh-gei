using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Models;

namespace OctoshiftCLI.Services;

public class GithubApi
{
    private readonly GithubClient _client;
    private readonly string _apiUrl;
    private readonly RetryPolicy _retryPolicy;
    private readonly ArchiveUploader _multipartUploader;

    public GithubApi(GithubClient client, string apiUrl, RetryPolicy retryPolicy, ArchiveUploader multipartUploader)
    {
        _client = client;
        _apiUrl = apiUrl;
        _retryPolicy = retryPolicy;
        _multipartUploader = multipartUploader;
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

        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/autolinks";

        var payload = new
        {
            key_prefix = keyPrefix,
            url_template = urlTemplate
        };

        await _client.PostAsync(url, payload);
    }

    public virtual async Task<List<(int Id, string KeyPrefix, string UrlTemplate)>> GetAutoLinks(string org, string repo)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/autolinks";

        return await _client.GetAllAsync(url)
                            .Select(al => ((int)al["id"], (string)al["key_prefix"], (string)al["url_template"]))
                            .ToListAsync();
    }

    public virtual async Task DeleteAutoLink(string org, string repo, int autoLinkId)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/autolinks/{autoLinkId}";

        await _client.DeleteAsync(url);
    }

    public virtual async Task<(string Id, string Slug)> CreateTeam(string org, string teamName)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams";
        var payload = new { name = teamName, privacy = "closed" };

        return await _retryPolicy.HttpRetry(async () =>
        {
            try
            {
                var response = await _client.PostAsync(url, payload);
                var data = JObject.Parse(response);
                return ((string)data["id"], (string)data["slug"]);
            }
            catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError)
            {
                // Before retrying, check if the team was actually created
                var teams = await GetTeams(org);
                var (Id, Name, Slug) = teams.FirstOrDefault(t => t.Name == teamName);
                if (Name != null)
                {
                    // Team exists, return its details instead of retrying
                    return (Id, Slug);
                }
                // Team doesn't exist, let the retry mechanism handle it
                throw;
            }
        }, ex => ex.StatusCode >= HttpStatusCode.InternalServerError);
    }

    public virtual async Task<IEnumerable<(string Id, string Name, string Slug)>> GetTeams(string org)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams";

        return await _client.GetAllAsync(url)
            .Select(t => ((string)t["id"], (string)t["name"], (string)t["slug"]))
            .ToListAsync();
    }

    public virtual async Task<IEnumerable<string>> GetTeamMembers(string org, string teamSlug)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams/{teamSlug.EscapeDataString()}/members?per_page=100";

        return await _retryPolicy.HttpRetry(async () => await _client.GetAllAsync(url).Select(x => (string)x["login"]).ToListAsync(),
                                        ex => ex.StatusCode == HttpStatusCode.NotFound);
    }

    public virtual async Task<IEnumerable<(string Name, string Visibility)>> GetRepos(string org)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/repos?per_page=100";

        return await _client.GetAllAsync(url).Select(x => ((string)x["name"], (string)x["visibility"])).ToListAsync();
    }

    public virtual async Task RemoveTeamMember(string org, string teamSlug, string member)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams/{teamSlug.EscapeDataString()}/memberships/{member.EscapeDataString()}";

        await _retryPolicy.Retry(() => _client.DeleteAsync(url));
    }

    public virtual async Task<string> GetLoginName()
    {
        var url = $"{_apiUrl}/graphql";

        var payload = new
        {
            query = "query{viewer{login}}"
        };

        try
        {
            return await _retryPolicy.Retry(async () =>
            {
                var data = await _client.PostGraphQLAsync(url, payload);

                return (string)data["data"]["viewer"]["login"];
            });
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException($"Failed to lookup the login for current user", ex);
        }
    }

    public virtual async Task<string> GetOrgMembershipForUser(string org, string member)
    {
        var url = $"{_apiUrl}/orgs/{org}/memberships/{member}";

        try
        {
            var response = await _client.GetAsync(url);

            var data = JObject.Parse(response);

            return (string)data["role"];
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) // Not a member
        {
            return null;
        }
    }

    public virtual async Task<bool> DoesRepoExist(string org, string repo)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}";
        try
        {
            await _client.GetNonSuccessAsync(url, HttpStatusCode.NotFound);
            return false;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.MovedPermanently)
        {
            return false;
        }
    }

    public virtual async Task<bool> DoesOrgExist(string org)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}";
        try
        {
            await _client.GetAsync(url);
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public virtual async Task AddTeamSync(string org, string teamName, string groupId, string groupName, string groupDesc)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams/{teamName.EscapeDataString()}/team-sync/group-mappings";
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
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams/{teamSlug.EscapeDataString()}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}";
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

    public virtual async Task<string> GetOrganizationDatabaseId(string org)
    {
        var url = $"{_apiUrl}/graphql";

        var payload = new
        {
            query = "query($login: String!) {organization(login: $login) { login, databaseId, name } }",
            variables = new { login = org }
        };

        try
        {
            return await _retryPolicy.Retry(async () =>
            {
                var data = await _client.PostGraphQLAsync(url, payload);

                return (string)data["data"]["organization"]["databaseId"];
            });
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException($"Failed to lookup the Organization database ID for organization '{org}'", ex);
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

        var data = await _client.PostGraphQLAsync(url, payload);
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

        var data = await _client.PostGraphQLAsync(url, payload);
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

        var data = await _client.PostGraphQLAsync(url, payload);
        return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
    }

    public virtual async Task<string> StartMigration(string migrationSourceId, string sourceRepoUrl, string orgId, string repo, string sourceToken, string targetToken, string gitArchiveUrl = null, string metadataArchiveUrl = null, bool skipReleases = false, string targetRepoVisibility = null, bool lockSource = false)
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
                    $targetRepoVisibility: String,
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
                        targetRepoVisibility: $targetRepoVisibility,
                        lockSource: $lockSource
                    }
                ) {
                    repositoryMigration {
                        id,
                        databaseId,
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
                targetRepoVisibility,
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
                            id,
                            databaseId
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

    public virtual async Task<string> StartBbsMigration(string migrationSourceId, string bbsRepoUrl, string orgId, string repo, string targetToken, string archiveUrl, string targetRepoVisibility = null)
    {
        return await StartMigration(
            migrationSourceId,
            bbsRepoUrl,  // source repository URL
            orgId,
            repo,
            "not-used",  // source access token
            targetToken,
            archiveUrl,
            "https://not-used",  // metadata archive URL
            false,  // skip releases
            targetRepoVisibility,
            false  // lock source
        );
    }

    public virtual async Task<(string State, string RepositoryName, int WarningsCount, string FailureReason, string MigrationLogUrl)> GetMigration(string migrationId)
    {
        var url = $"{_apiUrl}/graphql";

        var query = "query($id: ID!)";
        var gql = @"
                node(id: $id) {
                    ... on Migration {
                        id,
                        sourceUrl,
                        migrationLogUrl,
                        migrationSource {
                            name
                        },
                        state,
                        warningsCount,
                        failureReason,
                        repositoryName
                    }
                }";

        var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

        try
        {
            return await _retryPolicy.Retry(async () =>
            {
                var data = await _client.PostGraphQLAsync(url, payload);

                return (
                    State: (string)data["data"]["node"]["state"],
                    RepositoryName: (string)data["data"]["node"]["repositoryName"],
                    WarningsCount: (int)data["data"]["node"]["warningsCount"],
                    FailureReason: (string)data["data"]["node"]["failureReason"],
                    MigrationLogUrl: (string)data["data"]["node"]["migrationLogUrl"]);
            });
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException($"Failed to get migration state for migration {migrationId}", ex);
        }
    }

    public virtual async Task<(string MigrationLogUrl, string MigrationId)?> GetMigrationLogUrl(string org, string repo)
    {
        var url = $"{_apiUrl}/graphql";

        var query = "query ($org: String!, $repo: String!)";
        var gql = @"
                organization(login: $org) {
                    repositoryMigrations(last: 1, repositoryName: $repo) {
                        nodes {
                            id
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

                return nodes.Count == 0
                    // No matching migration was found
                    ? ((string MigrationLogUrl, string MigrationId)?)null
                    // A matching migration was found, which may or may not have a migration log URL. If there is no migration log, it's an empty string.
                    : (MigrationLogUrl: (string)nodes[0]["migrationLogUrl"], MigrationId: (string)nodes[0]["id"]);
            });
        }
        catch (Exception ex)
        {
            throw new OctoshiftCliException($"Failed to get migration log URL.", ex);
        }
    }

    public virtual async Task<int> GetIdpGroupId(string org, string groupName)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/external-groups";

        var group = await _client.GetAllAsync(url, data => (JArray)data["groups"])
            .SingleAsync(x => string.Equals((string)x["group_name"], groupName, StringComparison.OrdinalIgnoreCase));

        return (int)group["group_id"];
    }

    public virtual async Task<string> GetTeamSlug(string org, string teamName)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams";

        var response = await _client.GetAllAsync(url)
                                    .SingleAsync(x => ((string)x["name"]).ToUpper() == teamName.ToUpper());

        return (string)response["slug"];
    }

    public virtual async Task AddEmuGroupToTeam(string org, string teamSlug, int groupId)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/teams/{teamSlug.EscapeDataString()}/external-groups";
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
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}";
        await _client.DeleteAsync(url);
    }

    public virtual async Task<int> StartGitArchiveGeneration(string org, string repo)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/migrations";

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
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/migrations";

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
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/migrations/{archiveId}";

        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        return (string)data["state"];
    }

    public virtual async Task<string> GetArchiveMigrationUrl(string org, int archiveId)
    {
        var url = $"{_apiUrl}/orgs/{org.EscapeDataString()}/migrations/{archiveId}/archive";

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

    public virtual async Task<IEnumerable<Mannequin>> GetMannequinsByLogin(string orgId, string login)
    {
        var url = $"{_apiUrl}/graphql";

        var payload = GetMannequinsByLoginPayload(orgId, login);

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

    public virtual async Task<CreateAttributionInvitationResult> CreateAttributionInvitation(string orgId, string mannequinId, string targetUserId)
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

        return data.ToObject<CreateAttributionInvitationResult>();
    }

    public virtual async Task<ReattributeMannequinToUserResult> ReclaimMannequinSkipInvitation(string orgId, string mannequinId, string targetUserId)
    {
        var url = $"{_apiUrl}/graphql";
        var mutation = "mutation($orgId: ID!,$sourceId: ID!,$targetId: ID!)";
        var gql = @"
	            reattributeMannequinToUser(
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

        try
        {
            return await _retryPolicy.Retry(async () =>
            {
                var data = await _client.PostGraphQLAsync(url, payload);
                return data.ToObject<ReattributeMannequinToUserResult>();
            });
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("Field 'reattributeMannequinToUser' doesn't exist on type 'Mutation'"))
        {
            throw new OctoshiftCliException($"Reclaiming mannequins with the--skip - invitation flag is not enabled for your GitHub organization.For more details, contact GitHub Support.", ex);
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("Target must be a member"))
        {
            var result = new ReattributeMannequinToUserResult
            {
                Errors =
            [
              new ErrorData { Message = ex.Message }
            ]
            };
            return result;
        }
    }

    public virtual async Task<IEnumerable<GithubSecretScanningAlert>> GetSecretScanningAlertsForRepository(string org, string repo)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/secret-scanning/alerts?per_page=100";
        return await _client.GetAllAsync(url)
            .Select(secretAlert => BuildSecretScanningAlert(secretAlert))
            .ToListAsync();
    }

    public virtual async Task<IEnumerable<GithubSecretScanningAlertLocation>> GetSecretScanningAlertsLocations(string org, string repo, int alertNumber)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/secret-scanning/alerts/{alertNumber}/locations?per_page=100";
        return await _client.GetAllAsync(url)
            .Select(alertLocation => BuildSecretScanningAlertLocation(alertLocation))
            .ToListAsync();
    }

    public virtual async Task UpdateSecretScanningAlert(string org, string repo, int alertNumber, string state, string resolution = null, string resolutionComment = null)
    {
        if (!SecretScanningAlert.IsOpenOrResolved(state))
        {
            throw new ArgumentException($"Invalid value for {nameof(state)}");
        }

        if (SecretScanningAlert.IsResolved(state) && !SecretScanningAlert.IsValidDismissedReason(resolution))
        {
            throw new ArgumentException($"Invalid value for {nameof(resolution)}");
        }

        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/secret-scanning/alerts/{alertNumber}";

        object payload = state == SecretScanningAlert.AlertStateOpen ? new { state } : new { state, resolution, resolution_comment = resolutionComment };
        await _client.PatchAsync(url, payload);
    }

    public virtual async Task<IEnumerable<CodeScanningAnalysis>> GetCodeScanningAnalysisForRepository(string org, string repo, string branch = null)
    {
        var queryString = "per_page=100&sort=created&direction=asc";
        if (branch.HasValue())
        {
            queryString += $"&ref={branch.EscapeDataString()}";
        }

        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/analyses?{queryString}";

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

        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/alerts/{alertNumber}";

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
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/analyses/{analysisId}";
        // Need change the Accept header to application/sarif+json otherwise it will just be the analysis record
        var headers = new Dictionary<string, string>() { { "accept", "application/sarif+json" } };
        return await _client.GetAsync(url, headers);
    }

    public virtual async Task<string> UploadSarifReport(string org, string repo, string sarifReport, string commitSha, string sarifRef)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/sarifs";
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
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/sarifs/{sarifId.EscapeDataString()}";
        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        var errors = data["errors"]?.ToObject<string[]>() ?? Array.Empty<string>();
        return new() { Status = (string)data["processing_status"], Errors = errors };
    }

    public virtual async Task<string> GetDefaultBranch(string org, string repo)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}";

        var response = await _client.GetAsync(url);
        var data = JObject.Parse(response);

        return (string)data["default_branch"];
    }

    public virtual async Task<IEnumerable<CodeScanningAlert>> GetCodeScanningAlertsForRepository(string org, string repo, string branch = null)
    {
        var queryString = "per_page=100&sort=created&direction=asc";
        if (branch.HasValue())
        {
            queryString += $"&ref={branch.EscapeDataString()}";
        }
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/alerts?{queryString}";
        return await _client.GetAllAsync(url)
            .Select(BuildCodeScanningAlert)
            .ToListAsync();
    }

    public virtual async Task<IEnumerable<CodeScanningAlertInstance>> GetCodeScanningAlertInstances(string org, string repo, int alertNumber)
    {
        var url = $"{_apiUrl}/repos/{org.EscapeDataString()}/{repo.EscapeDataString()}/code-scanning/alerts/{alertNumber}/instances?per_page=100";
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

    public virtual async Task<bool> AbortMigration(string migrationId)
    {
        var url = $"{_apiUrl}/graphql";

        var query = @"
                mutation abortRepositoryMigration(
                    $migrationId: ID!,
                )";
        var gql = @"
                abortRepositoryMigration(
                    input: { 
                        migrationId: $migrationId
                    })
                   { success }";

        var payload = new
        {
            query = $"{query} {{ {gql} }}",
            variables = new
            {
                migrationId,
            },
            operationName = "abortRepositoryMigration"
        };

        try
        {
            var data = await _client.PostGraphQLAsync(url, payload);
            return (bool)data["data"]["abortRepositoryMigration"]["success"];
        }
        catch (OctoshiftCliException ex) when (ex.Message.Contains("Could not resolve to a node", StringComparison.OrdinalIgnoreCase))
        {
            throw new OctoshiftCliException($"Invalid migration id: {migrationId}", ex);
        }
    }

    public virtual async Task<string> UploadArchiveToGithubStorage(string orgDatabaseId, string archiveName, Stream archiveContent)
    {
        if (archiveContent is null)
        {
            throw new ArgumentNullException(nameof(archiveContent));
        }

        var uri = await _multipartUploader.Upload(archiveContent, archiveName, orgDatabaseId);

        return uri;
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

    private static object GetMannequinsByLoginPayload(string orgId, string login)
    {
        var query = "query($id: ID!, $first: Int, $after: String, $login: String)";
        var gql = @"
                node(id: $id) {
                    ... on Organization {
                        mannequins(first: $first, after: $after, login: $login) {
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
            variables = new { id = orgId, login }
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
            ResolutionComment = (string)secretAlert["resolution_comment"],
            SecretType = (string)secretAlert["secret_type"],
            Secret = (string)secretAlert["secret"],
            ResolverName = secretAlert["resolved_by"]?.Type != JTokenType.Null
                ? (string)secretAlert["resolved_by"]["login"]
                : null
        };

    private static GithubSecretScanningAlertLocation BuildSecretScanningAlertLocation(JToken alertLocation)
    {
        var details = alertLocation["details"];
        return new GithubSecretScanningAlertLocation
        {
            LocationType = (string)alertLocation["type"],
            Path = (string)details["path"],
            StartLine = (int?)details["start_line"] ?? 0,
            EndLine = (int?)details["end_line"] ?? 0,
            StartColumn = (int?)details["start_column"] ?? 0,
            EndColumn = (int?)details["end_column"] ?? 0,
            BlobSha = (string)details["blob_sha"],
            IssueTitleUrl = (string)details["issue_title_url"],
            IssueBodyUrl = (string)details["issue_body_url"],
            IssueCommentUrl = (string)details["issue_comment_url"],
            DiscussionTitleUrl = (string)details["discussion_title_url"],
            DiscussionBodyUrl = (string)details["discussion_body_url"],
            DiscussionCommentUrl = (string)details["discussion_comment_url"],
            PullRequestTitleUrl = (string)details["pull_request_title_url"],
            PullRequestBodyUrl = (string)details["pull_request_body_url"],
            PullRequestCommentUrl = (string)details["pull_request_comment_url"],
            PullRequestReviewUrl = (string)details["pull_request_review_url"],
            PullRequestReviewCommentUrl = (string)details["pull_request_review_comment_url"],
        };
    }
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
