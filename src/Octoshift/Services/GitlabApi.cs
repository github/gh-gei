using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class GitlabApi
{
    private readonly GitlabClient _client;
    private readonly string _gitlabBaseUrl;
    private readonly OctoLogger _log;

    public GitlabApi(GitlabClient client, string gitlabServerUrl, OctoLogger log)
    {
        _client = client;
        _gitlabBaseUrl = gitlabServerUrl?.TrimEnd('/');
        _log = log;
    }

    public virtual async Task<(string Version, bool Enterprise)> GetServerVersion()
    {
        var url = $"{_gitlabBaseUrl}/api/v4/version";

        var content = await _client.GetAsync(url);
        var data = JObject.Parse(content);

        return ((string)data["version"], (bool?)data["enterprise"] ?? false);
    }

    public virtual async Task LogServerVersion()
    {
        var (version, enterprise) = await GetServerVersion();
        if (!string.IsNullOrWhiteSpace(version))
        {
            var edition = enterprise ? "Enterprise" : "Community";
            _log?.LogInformation($"GitLab version: {version} ({edition} Edition)");
        }
    }

    public virtual async Task<string> StartExport(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}/export";

        var exportResponse = await _client.PostAsync(url, new { });
        var exportData = JObject.Parse(exportResponse);

        return (string)exportData["message"];
    }

    public virtual async Task<(string ExportStatus, string DownloadUrl)> GetExport(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}/export";

        var exportResponse = await _client.GetAsync(url);
        var exportData = JObject.Parse(exportResponse);

        return (
            (string)exportData["export_status"],
            (string)exportData["_links"]?["api_url"]
        );
    }

    public virtual async Task DownloadExportArchive(string groupPath, string projectPath, string file)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}/export/download";

        await _client.DownloadToFile(url, file);
    }

    public virtual async Task<IEnumerable<(long Id, string Path, string Name, bool Archived)>> GetProjects(string groupPath)
    {
        var encodedGroupPath = Uri.EscapeDataString(groupPath);
        var url = $"{_gitlabBaseUrl}/api/v4/groups/{encodedGroupPath}/projects?per_page=100";

        return await _client.GetAllAsync(url)
            .Select(x => ((long)x["id"], (string)x["path"], (string)x["name"], (bool)x["archived"]))
            .ToListAsync();
    }

    public virtual async Task<(long Id, string Path, string Name)> GetGroup(string groupPath)
    {
        var encodedGroupPath = groupPath.EscapeDataString();
        var url = $"{_gitlabBaseUrl}/api/v4/groups/{encodedGroupPath}";

        var groupResponse = await _client.GetAsync(url);
        var groupData = JObject.Parse(groupResponse);

        return (
            (long)groupData["id"],
            (string)groupData["full_path"],
            (string)groupData["name"]
        );
    }

    public virtual async Task<IEnumerable<(long Id, string Path, string Name)>> GetGroups()
    {
        var url = $"{_gitlabBaseUrl}/api/v4/groups?per_page=100";

        return await _client.GetAllAsync(url)
            .Select(x => ((long)x["id"], (string)x["full_path"], (string)x["name"]))
            .ToListAsync();
    }

    public virtual async Task<bool> GetIsProjectArchived(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}";

        var projectResponse = await _client.GetAsync(url);
        var projectData = JObject.Parse(projectResponse);

        return (bool)projectData["archived"];
    }

    public virtual async Task<DateTimeOffset?> GetRepositoryLatestCommitDate(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}/repository/commits?per_page=1";

        // Empty projects (no Git repo yet) return 404 here; treat as no commits.
        var commitsResponse = await _client.GetOrNullForNotFoundAsync(url);
        if (commitsResponse is null)
        {
            return null;
        }

        var commitsData = JArray.Parse(commitsResponse);
        var lastCommittedDate = (string)commitsData.First?["committed_date"];

        return string.IsNullOrWhiteSpace(lastCommittedDate) ? null : DateTimeOffset.Parse(lastCommittedDate);
    }

    public virtual async Task<(long RepositorySize, long AttachmentsSize)> GetRepositoryAndAttachmentsSize(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}?statistics=true";

        var projectResponse = await _client.GetAsync(url);
        var projectData = JObject.Parse(projectResponse);
        var projectStatistics = (JObject)projectData["statistics"];

        var repositorySize = (long)projectStatistics["repository_size"];
        var attachmentsSize = (long)projectStatistics["uploads_size"];

        return (repositorySize, attachmentsSize);
    }

    public virtual async Task<int> GetMergeRequestCount(string groupPath, string projectPath)
    {
        var encodedProjectPath = GetEncodedProjectPath(groupPath, projectPath);
        var url = $"{_gitlabBaseUrl}/api/v4/projects/{encodedProjectPath}/merge_requests?state=all&per_page=1&page=1";

        using var mrResponse = await _client.GetAsyncHttpResponseMessage(url);
        var mrTotal = mrResponse.Headers.GetValues("X-Total").Single();

        return int.Parse(mrTotal);
    }

    private static string GetEncodedProjectPath(string groupPath, string projectPath)
    {
        var pathWithNamespace = $"{groupPath}/{projectPath}";
        return pathWithNamespace.EscapeDataString();
    }
}
