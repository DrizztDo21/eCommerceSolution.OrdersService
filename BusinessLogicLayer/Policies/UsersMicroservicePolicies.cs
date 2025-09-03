using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using System;

namespace BusinessLogicLayer.Policies;
public class UsersMicroservicePolicies : IUsersMicroservicePolicies
{
    private readonly ILogger<UsersMicroservicePolicies> _logger;

    public UsersMicroservicePolicies(ILogger<UsersMicroservicePolicies> logger)
    {
        _logger = logger;
    }  

    public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        AsyncRetryPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                _logger.LogInformation($"Retry {retryAttempt} after {timespan.TotalSeconds} seconds ");
            });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        AsyncCircuitBreakerPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
           .CircuitBreakerAsync(
           handledEventsAllowedBeforeBreaking: 3,
           durationOfBreak: TimeSpan.FromMinutes(2),
           onBreak: (outcome, timespan) =>
           {
               _logger.LogInformation($"Circuit breaker is now open for {timespan.TotalMinutes} due to consecutive failures. The subsequent requests will be blocked");
           },
           onReset: () =>
           {
               _logger.LogInformation($"Circuit breaker closed");
           });

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetTimeOutPolicy()
    {
        AsyncTimeoutPolicy<HttpResponseMessage> policy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(1500));

        return policy;
    }

    public IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        var retryPolicy = GetRetryPolicy();
        var circuitBreakerPolicy = GetCircuitBreakerPolicy();
        var timeoutPolicy = GetTimeOutPolicy();

        AsyncPolicyWrap<HttpResponseMessage> combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);

        return combinedPolicy;
    }
}
