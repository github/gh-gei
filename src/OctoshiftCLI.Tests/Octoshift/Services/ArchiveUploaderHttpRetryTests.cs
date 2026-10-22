using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OctoshiftCLI.Services;
using Xunit;
using Xunit.Abstractions;

namespace OctoshiftCLI.Tests.Octoshift.Services;

public class ArchiveUploaderHttpRetryTests
{
    private const string UploadsUrl = "https://upload.test.invalid";
    private const string ArchiveUri = "gei://archive/synthetic";
    private readonly ITestOutputHelper _output;

    public ArchiveUploaderHttpRetryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Upload_Preserves_Payload_And_Recovers_From_Http_Timeouts(int timeouts)
    {
        using var handler = new UploadHandler(timeouts);
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var uploader = CreateUploader(httpClient);
        var payload = Enumerable.Range(1, 13).Select(value => (byte)value).ToArray();
        using var archive = new MemoryStream(payload);

        var result = await uploader.Upload(archive, "synthetic.tar", "1");

        Assert.Equal(ArchiveUri, result);
        Assert.Equal(timeouts + 2, handler.Contents.Count);
        Assert.Equal(timeouts, handler.TimeoutCount);
        Assert.Equal(0, handler.DisposedCount);
        Assert.Equal(payload, handler.CompletedParts.SelectMany(part => part).ToArray());
        Assert.Equal(new[] { 8, 5 }, handler.CompletedParts.Select(part => part.Length).ToArray());
        Assert.Equal(handler.Contents.Count, handler.Contents.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(1, handler.CompleteCount);
    }

    [Fact]
    public async Task Upload_Exhausts_Real_Timeouts_Without_Replacing_Them_With_Disposal_Errors()
    {
        using var handler = new UploadHandler(10);
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var uploader = CreateUploader(httpClient);
        using var archive = new MemoryStream(new byte[13]);

        var error = await Assert.ThrowsAsync<OctoshiftCliException>(
            () => uploader.Upload(archive, "synthetic.tar", "1"));

        Assert.Equal(6, handler.Contents.Count);
        Assert.Equal(6, handler.TimeoutCount);
        Assert.Equal(0, handler.DisposedCount);
        Assert.Empty(handler.CompletedParts);
        Assert.Equal(0, handler.CompleteCount);
        Assert.Contains(ExceptionChain(error), exception => exception is TaskCanceledException);
        Assert.DoesNotContain(ExceptionChain(error), exception => exception is ObjectDisposedException);
    }

    private static ArchiveUploader CreateUploader(HttpClient httpClient)
    {
        var logger = TestHelpers.CreateMock<OctoLogger>().Object;
        var retry = new RetryPolicy(logger) { _httpRetryInterval = 0, _retryInterval = 0 };
        var client = new GithubClient(
            logger, httpClient, null, retry, TestHelpers.CreateMock<DateTimeProvider>().Object, null);
        return new ArchiveUploader(
            client, UploadsUrl, logger, retry, TestHelpers.CreateMock<EnvironmentVariableProvider>().Object)
        {
            _streamSizeLimit = 8
        };
    }

    private static IEnumerable<Exception> ExceptionChain(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private sealed class UploadHandler(int remainingTimeouts) : HttpMessageHandler
    {
        public List<HttpContent> Contents { get; } = [];
        public List<byte[]> CompletedParts { get; } = [];
        public int TimeoutCount { get; private set; }
        public int DisposedCount { get; private set; }
        public int CompleteCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                return WithLocation("/parts/1");
            }

            if (request.Method == HttpMethod.Patch)
            {
                Contents.Add(request.Content);
                using var body = new MemoryStream();
                try
                {
                    await request.Content.CopyToAsync(body, cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    DisposedCount++;
                    throw;
                }

                if (remainingTimeouts > 0)
                {
                    remainingTimeouts--;
                    TimeoutCount++;
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                CompletedParts.Add(body.ToArray());
                return WithLocation(CompletedParts.Count == 1 ? "/parts/2" : "/complete");
            }

            if (request.Method == HttpMethod.Put)
            {
                CompleteCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"uri\":\"{ArchiveUri}\"}}")
                };
            }

            throw new InvalidOperationException($"Unexpected synthetic request: {request.Method}");
        }

        private static HttpResponseMessage WithLocation(string path)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("")
            };
            response.Headers.Location = new Uri(UploadsUrl + path);
            return response;
        }
    }
}
