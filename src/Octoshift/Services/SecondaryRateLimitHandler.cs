using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI.Services
{
    public sealed class SecondaryRateLimitHandler : DelegatingHandler
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialBackoff;
        private readonly TimeSpan _maxBackoff;

        public SecondaryRateLimitHandler(
            HttpMessageHandler innerHandler,
            int maxAttempts = 5,
            int initialBackoffSeconds = 60,
            int maxBackoffSeconds = 1800)
            : base(innerHandler)
        {
            _maxAttempts = maxAttempts;
            _initialBackoff = TimeSpan.FromSeconds(initialBackoffSeconds);
            _maxBackoff = TimeSpan.FromSeconds(maxBackoffSeconds);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) // renamed for CA1725
        {
            var attempt = 0;
            var delay = _initialBackoff;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode != HttpStatusCode.Forbidden)
                {
                    return response;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!body.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase))
                {
                    return response; // some other 403
                }

                attempt++;
                if (attempt >= _maxAttempts)
                {
                    return response;
                }

                // Respect Retry-After if present
                if (response.Headers.TryGetValues("Retry-After", out var values) &&
                    int.TryParse(System.Linq.Enumerable.FirstOrDefault(values), out var secs) &&
                    secs > 0)
                {
                    delay = TimeSpan.FromSeconds(secs);
                }

                // Secure jitter for CA5394
                var jitterMs = RandomNumberGenerator.GetInt32(0, 1000);
                await Task.Delay(delay + TimeSpan.FromMilliseconds(jitterMs), cancellationToken);

                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, _maxBackoff.TotalSeconds));
            }
        }
    }
}
