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

            var payload = $"{{ 'key_prefix': 'AB#', 'url_template': 'https://dev.azure.com/{adoOrg}/{adoTeamProject}/_workitems/edit/<num>/' }}";
            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PostAsync(url, body);
        }

        public async Task<string> CreateTeam(string org, string teamName)
        {
            var url = $"https://api.github.com/orgs/{org}/teams";
            var payload = $"{{ \"name\": \"{teamName}\", \"privacy\": \"closed\" }}";
            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

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
            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PatchAsync(url, body);
        }

        public async Task AddTeamToRepo(string org, string repo, string teamName, string role)
        {
            var url = $"https://api.github.com/orgs/{org}/teams/{teamName}/repos/{org}/{repo}";
            var payload = $"{{ 'permission':'{role}' }}";
            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            await _client.PutAsync(url, body);
        }

        public async Task<string> GetOrganizationId(string org)
        {
            var url = $"https://api.github.com/graphql";

            var payload = @"
{'query':'query(
  $login: String!
){
  organization(login: $login)
  {
    login
    id
    name
  }
}','variables':{'login':'GITHUB_ORG'}}'
";

            payload = payload.Replace("GITHUB_ORG", org);

            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["organization"]["id"];
        }

        public async Task<string> CreateMigrationSource(string orgId, string adoToken)
        {
            var url = $"https://api.github.com/graphql";

            var payload = @"
{'query':'mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $accessToken: String!, $type: MigrationSourceType!) {
  createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, accessToken: $accessToken, type: $type}) {
    migrationSource {
      id
      name
      url
      type
    }
  }
}
','variables':{'name':'Azure DevOps','url':'https://dev.azure.com','ownerId':'GITHUB_ORG_ID','type':'AZURE_DEVOPS','accessToken':'ADO_PAT'},'operationName':'createMigrationSource'}
";

            payload = payload.Replace("GITHUB_ORG_ID", orgId);
            payload = payload.Replace("ADO_PAT", adoToken);

            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["createMigrationSource"]["migrationSource"]["id"];
        }

        public async Task<string> StartMigration(string migrationSourceId, string adoRepoUrl, string orgId, string repo)
        {
            var url = $"https://api.github.com/graphql";

            var payload = @"
{'query':'mutation startRepositoryMigration(
  $sourceId: ID!,
  $ownerId: ID!,
  $sourceRepositoryUrl: URI!,
  $repositoryName: String!,
  $continueOnError: Boolean!
){
 startRepositoryMigration(input: {
 sourceId: $sourceId,
 ownerId: $ownerId,
 sourceRepositoryUrl: $sourceRepositoryUrl,
 repositoryName: $repositoryName,
 continueOnError: $continueOnError
  }) {
 repositoryMigration {
 id
 migrationSource {
 id
 name
 type
      }
 sourceUrl
 state
 failureReason
    }
  }
}
','variables':{'sourceId':'MIGRATION_SOURCE_ID','ownerId':'GITHUB_ORG_ID','sourceRepositoryUrl':'ADO_REPO_URL','repositoryName':'GITHUB_REPO','continueOnError':true},'operationName':'startRepositoryMigration'}
";

            payload = payload.Replace("MIGRATION_SOURCE_ID", migrationSourceId);
            payload = payload.Replace("GITHUB_ORG_ID", orgId);
            payload = payload.Replace("ADO_REPO_URL", adoRepoUrl);
            payload = payload.Replace("GITHUB_REPO", repo);

            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["startRepositoryMigration"]["repositoryMigration"]["id"];
        }

        public async Task<string> GetMigrationState(string migrationId)
        {
            var url = $"https://api.github.com/graphql";

            var payload = @"
{'query':'query(
  $id: ID!
){
 node(id: $id ) {
... on Migration {
 id
 sourceUrl
 migrationSource {
 name
      }
 state
 failureReason
    }
  }
}
','variables':{'id':'MIGRATION_ID'}}
";

            payload = payload.Replace("MIGRATION_ID", migrationId);

            var body = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, body);
            var data = JObject.Parse(response);

            return (string)data["data"]["node"]["state"];
        }
    }
}