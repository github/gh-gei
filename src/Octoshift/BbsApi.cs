using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI
{
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

        public virtual async Task<long> StartExport(string projectKey = "*", string slug = "*")
        {
            var url = $"{_bbsBaseUrl}/migration/exports";
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

             return(long)JObject.Parse(content)["id"];
        }

        public virtual async Task<string> GetExportState(long id)
        {
            var url = $"{_bbsBaseUrl}/migration/exports/{id}";

            var content = await _client.GetAsync(url);

            return (string)JObject.Parse(content)["state"];
        }
    }
}
