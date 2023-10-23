using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Services;

public class VersionChecker : IVersionProvider
{
    private string _latestVersion;
    private readonly HttpClient _httpClient;
    private readonly OctoLogger _log;

    public VersionChecker(HttpClient httpClient, OctoLogger log)
    {
        _httpClient = httpClient;
        _log = log;
    }

    public async Task<bool> IsLatest()
    {
        var currentVersion = Version.Parse(GetCurrentVersion());
        var latestVersion = Version.Parse(await GetLatestVersion());

        return currentVersion >= latestVersion;
    }

    public string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
    }

    public string GetVersionComments() =>
        CliContext.RootCommand.HasValue() && CliContext.ExecutingCommand.HasValue()
            ? $"({CliContext.RootCommand}/{CliContext.ExecutingCommand})"
            : null;

    public async Task<string> GetLatestVersion()
    {
        if (_latestVersion.IsNullOrWhiteSpace())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", GetCurrentVersion()));
            if (GetVersionComments() is { } comments)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
            }

            const string url = "https://raw.githubusercontent.com/github/gh-gei/main/LATEST-VERSION.txt";

            _log.LogVerbose($"HTTP GET: {url}");
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
            foreach (var header in response.Headers)
            {
                _log.LogDebug($"RESPONSE HEADER: {header.Key} = {string.Join(",", header.Value)}");
            }
            response.EnsureSuccessStatusCode();

            _latestVersion = content.TrimStart('v', 'V').Trim();
        }

        return _latestVersion;
    }
}
