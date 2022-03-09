using Newtonsoft.Json;

namespace Octoshift.Models
{
    public class AutoLink
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("key_prefix")]
        public string KeyPrefix { get; set; }
        [JsonProperty("url_template")]
        public string UrlTemplate { get; set; }
    }
}
