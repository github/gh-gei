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
            var srl = new SecondaryRateLimitHandler(
                inner,
                maxAttempts: 8,            // be more patient in Integration Tests
                initialBackoffSeconds: 30, // we'll honor Retry-After when provided
                maxBackoffSeconds: 900     // 15 minutes cap
            );
            return new HttpClient(srl, disposeHandler: true);
        }
    }
}
