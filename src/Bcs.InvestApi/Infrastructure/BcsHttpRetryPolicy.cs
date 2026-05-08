namespace Bcs.InvestApi.Infrastructure;

using System.Net;
using Polly;

internal static class BcsHttpRetryPolicy
{
    public static IAsyncPolicy<BcsHttpExchange> Create(BcsInvestApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTransportSettings();

        return Policy<BcsHttpExchange>
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<TaskCanceledException>(exception => exception.InnerException is TimeoutException)
            .OrResult(exchange => IsTransient(exchange.Response.StatusCode))
            .WaitAndRetryAsync(
                settings.HttpRetryAttempts,
                retryAttempt => CalculateDelay(settings.HttpRetryBaseDelay, retryAttempt),
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
