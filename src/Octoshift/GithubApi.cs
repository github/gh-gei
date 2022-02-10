using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI
{
    public class GithubApi
    {
        private readonly GithubClient _client;
        private readonly string _apiUrl;

        public GithubApi(GithubClient client, string apiUrl)
        {
            _client = client;
            _apiUrl = apiUrl;
        }

        public virtual async Task AddAutoLink(string org, string repo, string adoOrg, string adoTeamProject)
        {
            var url = $"{_apiUrl}/repos/{org}/{repo}/autolinks";

            var payload = new
            {
                key_prefix = "AB#",
                url_template = $"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/".Replace(" ", "%20")
            };

            await _client.PostAsync(url, payload);
        }

        public virtual async Task<string> CreateTeam(string org, string teamName)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams";
            var payload = new { name = teamName, privacy = "closed" };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public virtual async Task<IEnumerable<string>> GetTeamMembers(string org, string teamName)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamName}/members?per_page=100";

            return await _client.GetAllAsync(url).Select(x => (string)x["login"]).ToListAsync();
        }

        public virtual async Task<IEnumerable<string>> GetRepos(string org)
        {
            var url = $"{_apiUrl}/orgs/{org}/repos?per_page=100";

            return await _client.GetAllAsync(url).Select(x => (string)x["name"]).ToListAsync();
        }

        public virtual async Task RemoveTeamMember(string org, string teamName, string member)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamName}/memberships/{member}";

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

        public virtual async Task AddTeamToRepo(string org, string repo, string teamName, string role)
        {
            var url = $"{_apiUrl}/orgs/{org}/teams/{teamName}/repos/{org}/{repo}";
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

        public virtual async Task<string> CreateAdoMigrationSource(string orgId, string adoToken, string githubPat, bool ssh = false)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    name = "Azure DevOps Source",
                    url = "https://dev.azure.com",
                    ownerId = orgId,
                    type = "AZURE_DEVOPS",
                    accessToken = adoToken,
                    githubPat = !ssh ? githubPat : null
                },
                operationName = "createMigrationSource"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public virtual async Task<string> CreateGhecMigrationSource(string orgId, string sourceGithubPat, string targetGithubPat, bool ssh = false)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!, $githubPat: String)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type, githubPat: $githubPat}) { migrationSource { id, name, url, type } }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    name = "GHEC Source",
                    url = "https://github.com",
                    ownerId = orgId,
                    type = "GITHUB_ARCHIVE",
                    accessToken = sourceGithubPat,
                    githubPat = !ssh ? targetGithubPat : null
                },
                operationName = "createMigrationSource"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public virtual async Task<string> StartMigration(string migrationSourceId, string adoRepoUrl, string orgId, string repo, string gitArchiveUrl = "", string metadataArchiveUrl = "")
        {
            var url = $"{_apiUrl}/graphql";

            var query = "mutation startRepositoryMigration($sourceId: ID!, $ownerId: ID!, $sourceRepositoryUrl: URI!, $repositoryName: String!, $continueOnError: Boolean!, $gitArchiveUrl: String!, $metadataArchiveUrl: String!)";
            var gql = "startRepositoryMigration(input: { sourceId: $sourceId, ownerId: $ownerId, sourceRepositoryUrl: $sourceRepositoryUrl, repositoryName: $repositoryName, continueOnError: $continueOnError, gitArchiveUrl: $gitArchiveUrl, metadataArchiveUrl: $metadataArchiveUrl }) { repositoryMigration { id, migrationSource { id, name, type }, sourceUrl, state, failureReason } }";

            var payload = new
            {
                query = $"{query} {{ {gql} }}",
                variables = new
                {
                    sourceId = migrationSourceId,
                    ownerId = orgId,
                    sourceRepositoryUrl = adoRepoUrl,
                    repositoryName = repo,
                    continueOnError = true,
                    gitArchiveUrl,
                    metadataArchiveUrl
                },
                operationName = "startRepositoryMigration"
            };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["startRepositoryMigration"]["repositoryMigration"]["id"];
        }

        public virtual async Task<string> GetMigrationState(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["node"]["state"];
        }

        public virtual async Task<string> GetMigrationFailureReason(string migrationId)
        {
            var url = $"{_apiUrl}/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } }";

            var payload = new { query = $"{query} {{ {gql} }}", variables = new { id = migrationId } };

            var response = await _client.PostAsync(url, payload);
            var data = JObject.Parse(response);

            return (string)data["data"]["node"]["failureReason"];
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

            // TODO: Need to implement paging
            var response = await _client.GetAsync(url);
            var data = JArray.Parse(response);

            return (string)data.Children().Single(x => ((string)x["name"]).ToUpper() == teamName.ToUpper())["slug"];
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
    }
}
