using System;
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
                        if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            // We should not retry on an unathorized error; instead, log and bubble up the error
                            throw new OctoshiftCliException($"Unauthorized. Please check your token and try again", ex);
                        };
                    }
                    else
                    {
                        _log?.LogVerbose(ex.ToString());
                    }
                    _log?.LogVerbose("Retrying...");
                });
    }
}
