using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI
{
    public class HttpDownloadService
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly HttpClient _httpClient;

        public HttpDownloadService(OctoLogger log, HttpClient httpClient)
        {
            _log = log;
            _httpClient = httpClient;

            if (_httpClient is not null)
            {
                _httpClient.Timeout = TimeSpan.FromHours(1);
            }
        }

        public virtual async Task DownloadToFile(string url, string file)
        {
            _log.LogVerbose($"HTTP GET: {url}");

            using var response = await _httpClient.GetAsync(url);
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");

            response.EnsureSuccessStatusCode();

            var contents = await response.Content.ReadAsStringAsync();
            await WriteToFile(file, contents);
        }

        public virtual async Task<byte[]> DownloadToBytes(string url)
        {
            _log.LogVerbose($"HTTP GET: {url}");

            using var response = await _httpClient.GetAsync(url);
            _log.LogVerbose($"RESPONSE ({response.StatusCode}): <truncated>");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        public virtual async Task<Stream> DownloadStream(string url)
        {
            var client = new HttpClient();
            var rnd = new Random();
            var path = System.IO.Path.GetTempPath() + "MigrationStream" + rnd.Next(99999);

            while (File.Exists(path))
            {
                path += rnd.Next(9); // Extra measure to prevent filename duplicity error
            }

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            var streamToReadFrom = await response.Content.ReadAsStreamAsync();
            var streamToWriteTo = File.Open(path, FileMode.Create);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
            File.Delete(path); // Clean up customer disc
            return streamToWriteTo;

        }
    }
}
