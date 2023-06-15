using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

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

    public virtual async Task<long> StartExport(string projectKey, string slug)
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
                        projectKey,
                        slug
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
        return await _client.GetAllAsync(url)
            .Select(x => ((int)x["id"], (string)x["key"], (string)x["name"]))
            .ToListAsync();
    }

    public virtual async Task<IEnumerable<(int Id, string Slug, string Name)>> GetRepos(string projectKey)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos";
        return await _client.GetAllAsync(url)
            .Select(x => ((int)x["id"], (string)x["slug"], (string)x["name"]))
            .ToListAsync();
    }

    public virtual async Task<bool> GetIsRepositoryArchived(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo}?fields=archived";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);
        return (bool)data["archived"];
    }

    public virtual async Task<IEnumerable<(int Id, string Name)>> GetRepositoryPullRequests(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo}/pull-requests";
        return await _client.GetAllAsync(url)
            .Select(x => ((int)x["id"], (string)x["name"]))
            .ToListAsync();
    }

    public virtual async Task<JObject> GetRepositoryLatestCommit(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo}/commits?limit=1";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);
        return data;
    }

    public virtual async Task<ulong> GetRepositorySize(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo}/sizes";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);
        return (ulong)data["size"];
    }
}
