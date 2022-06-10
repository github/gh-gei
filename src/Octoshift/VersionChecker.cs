using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
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
            var curVersion = GetCurrentVersion();
            var latestVersion = await GetLatestVersion();

            curVersion = curVersion[..latestVersion.Length];

            return curVersion == latestVersion;
        }

        public string GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public string GetVersionComments() =>
            CliContext.RootCommand.HasValue() && CliContext.ExecutingCommand.HasValue()
                ? $"({CliContext.RootCommand}/{CliContext.ExecutingCommand})"
                : null;

        public async Task<string> GetLatestVersion()
        {
            if (_latestVersion.IsNullOrWhiteSpace())
            {
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", GetCurrentVersion()));
                if (GetVersionComments() is { } comments)
                {
                    _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(comments));
                }

                const string url = "https://api.github.com/repos/github/gh-gei/releases/latest";

                _log.LogVerbose($"HTTP GET: {url}");
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                _log.LogVerbose($"RESPONSE ({response.StatusCode}): {content}");
                response.EnsureSuccessStatusCode();
                var data = JObject.Parse(content);

                var latestTag = (string)data["tag_name"];

                _latestVersion = latestTag.TrimStart('v', 'V');
            }

            return _latestVersion;
        }
    }
}
