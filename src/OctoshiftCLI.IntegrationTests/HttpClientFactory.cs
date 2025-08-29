using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.IntegrationTests
{
    internal static class HttpClientFactory
    {
        // HttpClient(disposeHandler: true) disposes both handlers; we suppress CA2000 here once.
        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Ownership transferred to HttpClient via disposeHandler: true")]
        internal static HttpClient CreateSrlClient()
        {
            var inner = new HttpClientHandler();
            var srl = new SecondaryRateLimitHandler(inner);
            return new HttpClient(srl, disposeHandler: true);
        }
    }
}
