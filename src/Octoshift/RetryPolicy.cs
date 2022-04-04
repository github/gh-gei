using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;

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
            var policy = Policy.Handle(filter)
                               .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(1000), (ex, _) =>
                               {
                                   _log.LogVerbose($"Call failed with HTTP {((HttpRequestException)ex).StatusCode}, retrying...");
                               });

            return await policy.ExecuteAsync(func);
        }
    }
}
