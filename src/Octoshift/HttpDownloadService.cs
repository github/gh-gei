using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI
{
    public class HttpDownloadService
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly HttpClient _httpClient;

        public HttpDownloadService(HttpClient httpClient) => _httpClient = httpClient;

        public virtual async Task Download(string url, string file)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contents = await response.Content.ReadAsStringAsync();
            await WriteToFile(file, contents);
        }
    }
}
