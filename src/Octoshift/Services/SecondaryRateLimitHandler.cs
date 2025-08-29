using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI.Services
{
    public sealed class SecondaryRateLimitHandler : DelegatingHandler
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialBackoff;
        private readonly TimeSpan _maxBackoff;

        public SecondaryRateLimitHandler(HttpMessageHandler innerHandler,
                                         int maxAttempts = 5,
                                         int initialBackoffSeconds = 60,
                                         int maxBackoffSeconds = 1800)
            : base(innerHandler)
        {
            _maxAttempts = maxAttempts;
            _initialBackoff = TimeSpan.FromSeconds(initialBackoffSeconds);
            _maxBackoff = TimeSpan.FromSeconds(maxBackoffSeconds);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var attempt = 0;
            var delay = _initialBackoff;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var resp = await base.SendAsync(request, ct);

                if (resp.StatusCode != HttpStatusCode.Forbidden)
                    return resp;

                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!body.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase))
                    return resp;

                attempt++;
                if (attempt >= _maxAttempts)
                    return resp;

                if (resp.Headers.TryGetValues("Retry-After", out var vals) &&
                    int.TryParse(System.Linq.Enumerable.FirstOrDefault(vals), out var secs) &&
                    secs > 0)
                {
                    delay = TimeSpan.FromSeconds(secs);
                }

                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
                await Task.Delay(delay + jitter, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, _maxBackoff.TotalSeconds));
            }
        }
    }
}
