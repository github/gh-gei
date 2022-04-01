using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace OctoshiftCLI
{
    public class RetryPolicy
    {
        private readonly OctoLogger _log;

        public RetryPolicy(OctoLogger log)
        {
            _log = log;
        }

        public async Task<T> Retry<T>(Func<Task<T>> func, Func<HttpRequestException, bool> filter)
        {
            var delay = Backoff.LinearBackoff(TimeSpan.FromMilliseconds(200), retryCount: 5, fastFirst: true);
            var policy = Policy.Handle(filter)
                               .WaitAndRetryAsync(delay, (ex, _) =>
                               {
                                   _log.LogVerbose($"Call failed with HTTP {((HttpRequestException)ex).StatusCode}, retrying...");
                               });

            return await policy.ExecuteAsync(func);
        }
    }
}
