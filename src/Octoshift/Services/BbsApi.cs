using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

    public virtual async Task<(int Id, string Key, string Name)> GetProject(string projectKey)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}";
        var response = await _client.GetAsync(url);

        var project = JObject.Parse(response);
        return ((int)project["id"], (string)project["key"], (string)project["name"]);
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
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo.EscapeDataString()}?fields=archived";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);
        return (bool)data["archived"];
    }

    public virtual async Task<IEnumerable<(int Id, string Name)>> GetRepositoryPullRequests(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo.EscapeDataString()}/pull-requests?state=all";
        return await _client.GetAllAsync(url)
            .Select(x => ((int)x["id"], (string)x["name"]))
            .ToListAsync();
    }

    public virtual async Task<DateTime?> GetRepositoryLatestCommitDate(string projectKey, string repo)
    {
        var url = $"{_bbsBaseUrl}/rest/api/1.0/projects/{projectKey.EscapeDataString()}/repos/{repo.EscapeDataString()}/commits?limit=1";

        try
        {
            var response = await _client.GetAsync(url);
            var commit = JObject.Parse(response);

            if (commit?["values"] == null || !commit["values"].Any())
            {
                return null;
            }

            var authorTimestamp = (long)commit["values"][0]["authorTimestamp"];
            return DateTimeOffset.FromUnixTimeMilliseconds(authorTimestamp).DateTime;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<(ulong repoSize, ulong attachmentsSize)> GetRepositoryAndAttachmentsSize(string projectKey, string repo, string bbsUsername, string bbsPassword)
    {
        var url = $"{_bbsBaseUrl}/projects/{projectKey.EscapeDataString()}/repos/{repo.EscapeDataString()}/sizes";
        var response = await _client.GetAsync(url);

        var data = JObject.Parse(response);

        var repoSize = (ulong)data["repository"];
        var attachmentsSize = (ulong)data["attachments"];

        return (repoSize, attachmentsSize);
    }
}
