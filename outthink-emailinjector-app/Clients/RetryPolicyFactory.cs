using System.Net;
using Polly;

namespace OutThink.EmailInjectorApp.Clients;

/// <summary>
/// Provides a factory for creating retry policies using Polly, with support for Retry-After headers.
/// </summary>
public static class RetryPolicyFactory
{
    /// <summary>
    /// Creates an asynchronous retry policy for HTTP 429 (Too Many Requests) and 503 (Service Unavailable) responses.
    /// It respects the 'Retry-After' header if present, or uses exponential backoff.
    /// </summary>
    /// <param name="onRetry">
    /// Optional delegate to execute on each retry.
    /// Receives the delegate result, wait duration, retry attempt number, and execution context.
    /// </param>
    /// <returns>
    /// A Polly <see cref="IAsyncPolicy{HttpResponseMessage}"/> configured for retry logic.
    /// </returns>
    public static IAsyncPolicy<HttpResponseMessage> CreateWithRetryAfter(Action<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context>? onRetry = null)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.TooManyRequests || r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (retryAttempt, context) =>
                {
                    if (context.TryGetValue("response", out var responseObj) && responseObj is HttpResponseMessage response &&
                        response.Headers.TryGetValues("Retry-After", out var values) &&
                        int.TryParse(values.FirstOrDefault() ?? "10", out int seconds))
                    {
                        return TimeSpan.FromSeconds(seconds);
                    }

                    return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                },
                onRetry: onRetry ?? ((_, _, _, _) =>
                {
                    
                })
            );
    }
}