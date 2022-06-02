﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;

namespace OctoshiftCLI
{
    public class RetryPolicy
    {
        private readonly OctoLogger _log;
        internal readonly int _httpRetryInterval = 1000;
        internal int _retryOnResultInterval = 4000;

        public RetryPolicy(OctoLogger log)
        {
            _log = log;
        }

        public async Task<T> HttpRetry<T>(Func<Task<T>> func, Func<HttpRequestException, bool> filter)
        {
            var policy = Policy.Handle(filter)
                               .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(_httpRetryInterval), (ex, _) =>
                               {
                                   _log.LogVerbose($"Call failed with HTTP {((HttpRequestException)ex).StatusCode}, retrying...");
                               });

            return await policy.ExecuteAsync(func);
        }

        public async Task<PolicyResult<T>> RetryOnResult<T>(Func<Task<T>> func, T resultFilter, string retryLogMessage)
        {
            var policy = Policy.HandleResult(resultFilter)
                               .WaitAndRetryAsync(5, retry => retry * TimeSpan.FromMilliseconds(_retryOnResultInterval), (_, _) =>
                               {
                                   _log.LogVerbose(retryLogMessage ?? "Retrying...");
                               });

            return await policy.ExecuteAndCaptureAsync(func);
        }
    }
}
