using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI.Services;

public class GithubStatusApi
{
    private readonly BasicHttpClient _client;
    private const string GITHUB_STATUS_API_URL = "https://www.githubstatus.com/api/v2";

    public GithubStatusApi(BasicHttpClient client)
    {
        _client = client;
    }

    public virtual async Task<int> GetUnresolvedIncidentsCount()
    {
        var url = $"{GITHUB_STATUS_API_URL}/incidents/unresolved.json";
        var response = await _client.GetAsync(url);

        return JObject.Parse(response)["incidents"].Count();
    }
}
