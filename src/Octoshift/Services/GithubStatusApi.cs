using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI.Services;

public class GithubStatusApi
{
    private readonly GithubStatusClient _client;
    private readonly string _baseUrl;

    public GithubStatusApi(GithubStatusClient client, string baseUrl)
    {
        _client = client;
        _baseUrl = baseUrl;
    }

    public virtual async Task<int> GetUnresolvedIncidentsCount()
    {
        var url = $"{_baseUrl}/incidents/unresolved.json";
        var response = await _client.GetAsync(url);

        return JObject.Parse(response)["incidents"].Count();
    }
}
