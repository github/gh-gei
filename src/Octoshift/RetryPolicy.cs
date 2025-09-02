using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OctoshiftCLI.Services;
using Polly;
using Polly.Retry;

namespace OctoshiftCLI
{
    public class RetryPolicy
    {
        private readonly OctoLogger _log;
        internal int _httpRetryInterval = 1000;
        internal int _retryInterval = 4000;

        public RetryPolicy(OctoLogger log)
        {
            _log = log;
        }

        /// <summary>
        /// NEW: Minimal overload for HTTP calls that returns HttpResponseMessage so we can
        /// honor Retry-After and X-RateLimit-* headers for secondary rate limits.
        /// Usage: var resp = await _retryPolicy.HttpRetry(() => _httpClient.SendAsync(request));
        /// </summary>
        public async Task<HttpResponseMessage> HttpRetry(Func<Task<HttpResponseMessage>> func)
        {
            var policy = Policy
                .HandleResult<HttpResponseMessage>(r =>
                {
                    var sc = (int)r.StatusCode;
                    if (sc != 403 && sc != 429) return false;

                    // Treat Retry-After or X-RateLimit-Remaining: 0 as secondary-rate limiting signals
                    if (r.Headers.RetryAfter != null) return true;
                    if (r.Headers.TryGetValues("X-RateLimit-Remaining", out var remain) &&
                        remain?.FirstOrDefault() == "0") return true;

                    // Fallback: any 403/429 without headers still gets backoff per docs
                    return true;
                })
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: async (attempt, outcome, ctx) =>
                    {
                        var r = outcome.Result;

                        // 1) Honor Retry-After header
                        var ra = r.Headers.RetryAfter?.Delta;
                        if (ra.HasValue) return ra.Value;

                        // 2) If remaining == 0, wait until reset
                        if (r.Headers.TryGetValues("X-RateLimit-Remaining", out var remainVals) &&
                            remainVals?.FirstOrDefault() == "0" &&
                            r.Headers.TryGetValues("X-RateLimit-Reset", out var resetVals) &&
                            long.TryParse(resetVals?.FirstOrDefault(), out var resetEpoch))
                        {
                            var resetAt = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);
                            var wait = resetAt - DateTimeOffset.UtcNow;
                            if (wait > TimeSpan.Zero) return wait;
                        }

                        // 3) Otherwise: at least 1 minute, exponential thereafter (1,2,4,8,...)
                        var minutes = Math.Max(1, Math.Pow(2, attempt - 1));
                        return TimeSpan.FromMinutes(minutes);
                    },
                    onRetryAsync: async (outcome, wait, attempt, ctx) =>
                    {
                        var r = outcome.Result;
                        string body = null;
                        try { body = r.Content != null ? await r.Content.ReadAsStringAsync() : null; } catch { /* ignore */ }

                        _log?.LogVerbose(
                            $"Secondary rate limit (HTTP {(int)r.StatusCode}). " +
                            $"Retrying in {wait.TotalSeconds:N0}s (attempt {attempt}). " +
                            $"Retry-After={r.Headers.RetryAfter?.Delta?.TotalSeconds}, " +
                            $"X-RateLimit-Remaining={TryHeader(r, "X-RateLimit-Remaining")}, " +
                            $"X-RateLimit-Reset={TryHeader(r, "X-RateLimit-Reset")}. " +
                            $"Body={(body?.Length > 200 ? body.Substring(0, 200) + "…" : body)}");
                    });

            var resp = await policy.ExecuteAsync(func);

            if (!resp.IsSuccessStatusCode)
            {
                var finalBody = (resp.Content != null) ? await resp.Content.ReadAsStringAsync() : null;
                throw new OctoshiftCliException($"HTTP {(int)resp.StatusCode} after retries. Body={finalBody}");
            }

            return resp;

            static string TryHeader(HttpResponseMessage r, string name)
                => r.Headers.TryGetValues(name, out var vals) ? vals?.FirstOrDefault() : null;
        }

        // Existing overload preserved (exception-based)
        public async Task<T> HttpRetry<T>(Func<Task<T>> func, Func<HttpRequestException, bool> filter)
        {
            var policy = Policy.Handle(filter)
                               .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(_httpRetryInterval), (ex, _) =>
                               {
                                   _log?.LogVerbose($"Call failed with HTTP {((HttpRequestException)ex).StatusCode}, retrying...");
                               });

            return await policy.ExecuteAsync(func);
        }

        public async Task<PolicyResult<T>> RetryOnResult<T>(Func<Task<T>> func, Func<T, bool> resultPredicate, string retryLogMessage = null)
        {
            var policy = Policy.HandleResult(resultPredicate)
                               .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(_retryInterval), (_, _) =>
                               {
                                   _log?.LogVerbose(retryLogMessage ?? "Retrying...");
                               });

            return await policy.ExecuteAndCaptureAsync(func);
        }

        public async Task Retry(Func<Task> func) => await CreateRetryPolicyForException<Exception>().ExecuteAsync(func);

        public async Task<T> Retry<T>(Func<Task<T>> func) => await CreateRetryPolicyForException<Exception>().ExecuteAsync(func);

        private AsyncRetryPolicy CreateRetryPolicyForException<TException>() where TException : Exception => Policy
            .Handle<TException>()
            .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(_retryInterval), (Exception ex, TimeSpan ts, Context ctx) =>
            {
                if (ex is HttpRequestException httpEx)
                {
                    _log?.LogVerbose($"[HTTP ERROR {(int?)httpEx.StatusCode}] {ex}");
                    if (httpEx.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // We should not retry on an unauthorized error; instead, log and bubble up the error
                        throw new OctoshiftCliException("Unauthorized. Please check your token and try again", ex);
                    }
                }
                else
                {
                    _log?.LogVerbose(ex.ToString());
                }
                _log?.LogVerbose("Retrying...");
            });
    }
}
