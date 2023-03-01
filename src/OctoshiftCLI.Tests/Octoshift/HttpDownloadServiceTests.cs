using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace OctoshiftCLI.Tests.Octoshift
{
    public class HttpDownloadServiceTests
    {
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();

        private readonly HttpDownloadService _service;

        public HttpDownloadServiceTests()
        {
            _service = new HttpDownloadService(_mockOctoLogger.Object, new System.Net.Http.HttpClient());
        }

        [Fact]
        public async Task Download_Large_File()
        {
            var client = new HttpClient();

            var url = "https://url.large.file";
            var path = "C:\\testing\\gopro.mp4";

            HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
            {
                using (Stream streamToWriteTo = File.Open(path, FileMode.Create))
                {
                    await streamToReadFrom.CopyToAsync(streamToWriteTo);
                }
            }

            //await _service.DownloadToFile(url, path);
        }
    }
}
