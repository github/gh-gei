using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI;

public class BbsApi
{
    private readonly BbsClient _client;
    private readonly string _bbsBaseUrl;
    private readonly OctoLogger _log;

    public BbsApi(BbsClient client, string bbsServerUrl, OctoLogger log)
    {
        _client = client;
        _bbsBaseUrl = bbsServerUrl?.TrimEnd('/');
        _log = log;
    }

    public virtual async Task<string> GetServerVersion()
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/application-properties";

        var content = await _client.GetAsync(url);

        return (string)JObject.Parse(content)["version"];
    }

    public virtual async Task<long> StartExport(string projectKey = "*", string slug = "*")
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/migration/exports";
        var payload = new
        {
            repositoriesRequest = new
            {
                includes = new[]
                {
                        new
                        {
                            projectKey = projectKey ?? "*",
                            slug = slug ?? "*"
                        }
                    }
            }
        };

        var content = await _client.PostAsync(url, payload);

        return (long)JObject.Parse(content)["id"];
    }

    public virtual async Task<(string State, string Message, int Percentage)> GetExport(long id)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/migration/exports/{id}";

        var content = await _client.GetAsync(url);
        var data = JObject.Parse(content);

        return (
            (string)data["state"],
            (string)data["progress"]["message"],
            (int)data["progress"]["percentage"]
        );
    }

    public virtual async Task<IEnumerable<(int Id, string Key, string Name)>> GetProjects()
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects";
        var content = await _client.GetAsync(url);
        var data = JObject.Parse(content);

        return data["values"].Select(x => ((int)x["id"], (string)x["key"], (string)x["name"])).ToList();
    }

    public virtual async Task<IEnumerable<(int Id, string Slug, string Name)>> GetRepos(string projectKey)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey}/repos";
        var content = await _client.GetAsync(url);
        var data = JObject.Parse(content);

        return data["values"].Select(x => ((int)x["id"], (string)x["slug"], (string)x["name"])).ToList();
    }
}
