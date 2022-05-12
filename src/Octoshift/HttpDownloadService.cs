using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;

namespace Octoshift
{
    public class HttpDownloadService
    {
        public static async Task<bool> Download(string url, string file)
        {
            HttpClient client = new HttpClient();

            try
            {
                using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open(file, FileMode.Create);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);
            }
            catch (HttpRequestException)
            {
                return false;
            }
            finally
            {
                client.Dispose();
            }

            return true;
        }
    }
}
