using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    public class GithubApi
    {
        private readonly GithubClient _client;

        public GithubApi(string token)
        {
            _client = new GithubClient(token);
        }

        public async Task AddAutoLink(string org, string repo, string adoOrg, string adoTeamProject)
        {
            var url = $"https://api.github.com/repos/{org}/{repo}/autolinks";

            var payload = $"{{ \"key_prefix\": \"AB#\", \"url_template\": \"https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/\" }}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PostAsync(url, body);
        }

        public async Task<string> CreateTeam(string org, string teamName)
        {
            var url = $"https://api.github.com/orgs/{org}/teams";
            var payload = $"{{ \"name\": \"{teamName}\", \"privacy\": \"closed\" }}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["id"];
        }

        public async Task<IEnumerable<string>> GetTeamMembers(string org, string teamName)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/members";

            var response = await _client.GetAsync(url);
            var data = JArray.Parse(response);

            return data.Children().Select(x => (string)x["login"]).ToList();
        }

        public async Task RemoveTeamMember(string org, string teamName, string member)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/memberships/{member}";

            await _client.DeleteAsync(url);
        }

        public async Task<(string id, string name, string description)> GetIdpGroup(string org, string idpGroupName)
        {
            var url = $"https://api.github.com/orgs/{org}/team-sync/groups";

            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return data["groups"].Children()
                                 .Select(x => (id: (string)x["group_id"], name: (string)x["group_name"], description: (string)x["group_description"]))
                                 .Single(x => x.name.ToLower() == idpGroupName.ToLower());
        }

        public async Task AddTeamSync(string org, string teamName, string groupId, string groupName, string groupDesc)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/team-sync/group-mappings";
            var payload = $"{{ 'groups': [{{ 'group_id':'{groupId}', 'group_name':'{groupName}','group_description':'{groupDesc}' }}] }}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PatchAsync(url, body);
        }

        public async Task AddTeamToRepo(string org, string repo, string teamName, string role)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/repos/{org}/{repo}";
            var payload = $"{{ \"permission\":\"{role}\" }}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PutAsync(url, body);
        }

        public async Task<string> GetOrganizationId(string org)
        {
            var url = $"https://api.github.com/graphql";

            // TODO: this is super ugly, need to find a graphql library to make this code nicer
            var payload = $"{{\"query\":\"query($login: String!){{organization(login: $login) {{ login, id, name }} }}\",\"variables\":{{\"login\":\"{org}\"}}}}";

            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["organization"]["id"];
        }

        public async Task<string> CreateMigrationSource(string orgId, string adoToken)
        {
            var url = $"https://api.github.com/graphql";

            var query = "mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!)";
            var gql = "createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type}) { migrationSource { id, name, url, type } }";
            var variables = $"{{\"name\":\"Azure DevOps Source\",\"url\":\"https://dev.azure.com\",\"ownerId\":\"{orgId}\",\"type\":\"AZURE_DEVOPS\",\"accessToken\":\"{adoToken}\"}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables},\"operationName\":\"createMigrationSource\"}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public async Task<string> StartMigration(string migrationSourceId, string adoRepoUrl, string orgId, string repo)
        {
            var url = $"https://api.github.com/graphql";

            var query = "mutation startRepositoryMigration($sourceId: ID!, $ownerId: ID!, $sourceRepositoryUrl: URI!, $repositoryName: String!, $continueOnError: Boolean!)";
            var gql = "startRepositoryMigration(input: { sourceId: $sourceId, ownerId: $ownerId, sourceRepositoryUrl: $sourceRepositoryUrl, repositoryName: $repositoryName, continueOnError: $continueOnError }) { repositoryMigration { id, migrationSource { id, name, type }, sourceUrl, state, failureReason } }";
            var variables = $"{{\"sourceId\":\"{migrationSourceId}\",\"ownerId\":\"{orgId}\",\"sourceRepositoryUrl\":\"{adoRepoUrl}\",\"repositoryName\":\"{repo}\",\"continueOnError\":true}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables},\"operationName\":\"startRepositoryMigration\"}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["startRepositoryMigration"]["repositoryMigration"]["id"];
        }

        public async Task<string> GetMigrationState(string migrationId)
        {
            var url = $"https://api.github.com/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } }";
            var variables = $"{{\"id\":\"{migrationId}\"}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables}}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["node"]["state"];
        }

        public async Task<string> GetMigrationFailureReason(string migrationId)
        {
            var url = $"https://api.github.com/graphql";

            var query = "query($id: ID!)";
            var gql = "node(id: $id) { ... on Migration { id, sourceUrl, migrationSource { name }, state, failureReason } }";
            var variables = $"{{\"id\":\"{migrationId}\"}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables}}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["node"]["failureReason"];
        }

        public async Task<int> GetIdpGroupId(string org, string groupName)
        {
            var url = $"https://api.github.com/orgs/{org}/external-groups";

            // TODO: Need to implement paging
            var response = await _client.GetAsync(url);
            var data = JObject.Parse(response);

            return (int)data["groups"].Children().Single(x => ((string)x["group_name"]).ToUpper() == groupName.ToUpper())["group_id"];
        }

        public async Task<string> GetTeamSlug(string org, string teamName)
        {
            var url = $"https://api.github.com/orgs/{org}/teams";

            // TODO: Need to implement paging
            var response = await _client.GetAsync(url);
            var data = JArray.Parse(response);

            return (string)data.Children().Single(x => ((string)x["name"]).ToUpper() == teamName.ToUpper())["slug"];
        }

        public async Task AddEmuGroupToTeam(string org, string teamSlug, int groupId)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamSlug}/external-groups";
            var payload = $"{{ \"group_id\": {groupId} }}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PatchAsync(url, body);
        }

            public async Task<string> GrantMigratorRole(string org, string actor, string actorType)
        {
            var url = $"https://api.github.com/graphql";

            var query = "mutation grantMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! )";
            var gql = "grantMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }";
            var variables = $"{{\"organizationId\":\"{org}\", \"actor\":\"{actor}\", \"actor_type\":\"{actorType}\"}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables},\"operationName\":\"grantMigratorRole\"}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            try 
            {
                var response = await _client.PostAsync(url, body);
                var data = JObject.Parse(response);
                
                return (string)data["data"]["grantMigratorRole"]["success"];
            } 
            catch
            {
                return "False";
            }
        }

         public async Task<string> RevokeMigratorRole(string org, string actor, string actorType)
        {
            var url = $"https://api.github.com/graphql";

            var query = "mutation revokeMigratorRole ( $organizationId: ID!, $actor: String!, $actor_type: ActorType! )";
            var gql = "revokeMigratorRole( input: {organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }";
            var variables = $"{{\"organizationId\":\"{org}\", \"actor\":\"{actor}\", \"actor_type\":\"{actorType}\"}}";

            var payload = $"{{\"query\":\"{query} {{ {gql} }}\",\"variables\":{variables},\"operationName\":\"revokeMigratorRole\"}}";
            using var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            try 
            {
                var response = await _client.PostAsync(url, body);
                var data = JObject.Parse(response);
                
                return (string)data["data"]["revokeMigratorRole"]["success"];
            } 
            catch
            {
                return "False";
            }

        }
    }
}