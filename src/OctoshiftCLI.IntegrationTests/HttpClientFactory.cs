using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.IntegrationTests
{
    internal static class HttpClientFactory
    {
        // Handlers are disposed by HttpClient(disposeHandler: true); suppress CA2000 here once.
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Ownership transferred to HttpClient via disposeHandler: true")]
        internal static HttpClient CreateSrlClient()
        {
#if NET6_0_OR_GREATER
            var sockets = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip,
                PooledConnectionLifetime = System.TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 2,   // keep concurrency low to avoid bursty 403s
                UseCookies = false
            };
            var srl = new SecondaryRateLimitHandler(sockets);
#else
            var inner = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.GZip
            };
            var srl = new SecondaryRateLimitHandler(inner);
#endif
            var client = new HttpClient(srl, disposeHandler: true);

            // Ensure we always send a UA (GitHub requires it) — your GithubClient also sets one, but this is harmless.
            if (!client.DefaultRequestHeaders.UserAgent.TryParseAdd("octoshift-cli-integration-tests"))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "octoshift-cli-integration-tests");
            }

            return client;
        }
    }
}
