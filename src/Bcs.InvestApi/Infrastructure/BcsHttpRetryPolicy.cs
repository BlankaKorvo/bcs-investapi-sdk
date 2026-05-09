namespace Bcs.InvestApi.Infrastructure;

using System.Net;
using Polly;

internal static class BcsHttpRetryPolicy
{
    public static IAsyncPolicy<BcsHttpExchange> CreateFor(
        BcsInvestApiSettings settings,
        BcsRequestSafety safety)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return safety switch
        {
            BcsRequestSafety.IdempotentRead or BcsRequestSafety.IdempotentQueryPost =>
                CreateRetrying(settings.HttpRetryAttempts, settings.HttpRetryBaseDelay),
            BcsRequestSafety.NonIdempotentCommand =>
                Policy.NoOpAsync<BcsHttpExchange>(),
            BcsRequestSafety.TokenRefresh =>
                Policy.NoOpAsync<BcsHttpExchange>(),
            _ => throw new ArgumentOutOfRangeException(nameof(safety), safety, null),
        };
    }

    public static IAsyncPolicy<BcsHttpExchange> CreateForRead(BcsInvestApiSettings settings) =>
        CreateFor(settings, BcsRequestSafety.IdempotentRead);

    public static IAsyncPolicy<BcsHttpExchange> CreateForIdempotentQueryPost(BcsInvestApiSettings settings) =>
        CreateFor(settings, BcsRequestSafety.IdempotentQueryPost);

    public static IAsyncPolicy<BcsHttpExchange> CreateForCommand(BcsInvestApiSettings settings) =>
        CreateFor(settings, BcsRequestSafety.NonIdempotentCommand);

    public static IAsyncPolicy<BcsHttpExchange> CreateForTokenRefresh(BcsInvestApiSettings settings) =>
        CreateFor(settings, BcsRequestSafety.TokenRefresh);

    private static IAsyncPolicy<BcsHttpExchange> CreateRetrying(int retryAttempts, TimeSpan retryBaseDelay)
    {
        return Policy<BcsHttpExchange>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>(exception => exception.InnerException is TimeoutException)
            .OrResult(exchange => IsTransient(exchange.Response.StatusCode))
            .WaitAndRetryAsync(
                retryAttempts,
                retryAttempt => CalculateDelay(retryBaseDelay, retryAttempt),
                onRetry: (outcome, _, _, _) => outcome.Result?.Dispose());
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;

    private static TimeSpan CalculateDelay(TimeSpan baseDelay, int retryAttempt)
    {
        if (baseDelay == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var multiplier = Math.Pow(2, retryAttempt - 1);
        var delayMilliseconds = baseDelay.TotalMilliseconds * multiplier;

        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }
}
