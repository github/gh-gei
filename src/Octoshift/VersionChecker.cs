using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI
{
    public class VersionChecker
    {
        private string _latestVersion;

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

        public async Task<string> GetLatestVersion()
        {
            if (string.IsNullOrWhiteSpace(_latestVersion))
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", GetCurrentVersion()));

                var url = "https://api.github.com/repos/github/gh-gei/releases/latest";

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(content);

                var latestTag = (string)data["tag_name"];

                _latestVersion = latestTag.TrimStart('v', 'V');
            }

            return _latestVersion;
        }
    }
}
