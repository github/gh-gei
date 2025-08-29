// src/Octoshift/Services/SecondaryRateLimitHandler.cs
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI.Services
{
    /// <summary>
    /// Handles GitHub secondary rate-limit / abuse-detection 403s by
    /// respecting Retry-After (when present) and otherwise using
    /// exponential backoff with jitter. Clones the request to safely
    /// resend POSTs with content.
    /// </summary>
    public sealed class SecondaryRateLimitHandler : DelegatingHandler
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _initialBackoff;
        private readonly TimeSpan _maxBackoff;

        public SecondaryRateLimitHandler(
            HttpMessageHandler innerHandler,
            int maxAttempts = 6,
            int initialBackoffSeconds = 15,
            int maxBackoffSeconds = 900)
            : base(innerHandler)
        {
            _maxAttempts = maxAttempts;
            _initialBackoff = TimeSpan.FromSeconds(initialBackoffSeconds);
            _maxBackoff = TimeSpan.FromSeconds(maxBackoffSeconds);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var attempt = 0;
            var delay = _initialBackoff;

            // Buffer original content (if any) so we can safely clone the request for retries.
            byte[] bufferedContent = null;
            string contentType = null;

            if (request.Content != null)
            {
                bufferedContent = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                contentType = request.Content.Headers.ContentType?.ToString();
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var cloned = CloneRequest(request, bufferedContent, contentType);
                var response = await base.SendAsync(cloned, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.Forbidden)
                {
                    return response;
                }

                // Look for known secondary rate limit / abuse messages in body.
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var looksLikeSecondary =
                    body.Contains("secondary rate limit", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("abuse detection", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

                if (!looksLikeSecondary)
                {
                    return response; // Some other 403 — don't loop.
                }

                attempt++;
                if (attempt >= _maxAttempts)
                {
                    return response; // Give up; caller/policy will surface it.
                }

                // Prefer server-provided delay if present.
                if (response.Headers.TryGetValues("Retry-After", out var values) &&
                    int.TryParse(values.FirstOrDefault(), out var secs) &&
                    secs > 0)
                {
                    delay = TimeSpan.FromSeconds(secs);
                }

                // Add secure jitter (0–1000ms) to spread retries.
                var jitterMs = RandomNumberGenerator.GetInt32(0, 1000);
                var totalDelay = delay + TimeSpan.FromMilliseconds(jitterMs);

                await Task.Delay(totalDelay, cancellationToken).ConfigureAwait(false);

                // Exponential backoff, capped.
                var nextSeconds = Math.Min(delay.TotalSeconds * 2, _maxBackoff.TotalSeconds);
                delay = TimeSpan.FromSeconds(nextSeconds);
                // Loop and retry with a freshly cloned request.
            }
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[] bufferedContent, string contentType)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri)
            {
                Version = original.Version,
                VersionPolicy = original.VersionPolicy
            };

            // Copy headers
            foreach (var header in original.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Copy content
            if (bufferedContent != null)
            {
                var content = new ByteArrayContent(bufferedContent);
                if (!string.IsNullOrEmpty(contentType))
                {
                    content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                }

                if (original.Content != null)
                {
                    foreach (var h in original.Content.Headers)
                    {
                        if (!string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                        }
                    }
                }

                clone.Content = content;
            }

#if NET6_0_OR_GREATER
            foreach (var opt in original.Options)
            {
                clone.Options.Set(new(opt.Key), opt.Value);
            }
#endif

            return clone;
        }
    }
}
