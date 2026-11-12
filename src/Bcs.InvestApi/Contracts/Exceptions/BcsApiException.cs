namespace Bcs.InvestApi.Contracts.Exceptions;

using System.Net;
using Bcs.InvestApi.Contracts.Errors;

/// <summary>
/// Thrown when a BCS API endpoint returns a non-success HTTP status.
/// </summary>
/// <remarks>
/// The SDK does not retry. When <see cref="StatusCode"/> is
/// <see cref="HttpStatusCode.Unauthorized"/>, the cached access token is dropped before this exception
/// is thrown, so the failing call still surfaces the 401 but the next call transparently obtains a
/// fresh token. Deciding whether to retry that next call is the caller's responsibility.
/// </remarks>
public sealed class BcsApiException : Exception
{
    public BcsApiException(
        HttpStatusCode statusCode,
        string responseBody,
        string? endpoint = null,
        BcsApiErrorResponse? apiError = null,
        TimeSpan? retryAfter = null)
        : base(BuildMessage(statusCode, endpoint, apiError))
    {
        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryAfter), retryAfter, "Retry-After must not be negative.");
        }

        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
        Endpoint = endpoint;
        ApiError = apiError;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }

    public string? Endpoint { get; }

    public BcsApiErrorResponse? ApiError { get; }

    /// <summary>
    /// How long the server asks to wait before the next request (the <c>Retry-After</c> header of the answer); <c>null</c> —
    /// the answer has no such header. The SDK does not wait itself.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The exception of a non-success answer, with the server's wait from its headers.</summary>
    internal static BcsApiException FromResponse(
        HttpResponseMessage response,
        string responseBody,
        string? endpoint,
        BcsApiErrorResponse? apiError) =>
        new(response.StatusCode, responseBody, endpoint, apiError, ReadRetryAfter(response));

    // Retry-After (RFC 9110, 10.2.3): seconds or a date; the date is counted from the answer's Date when it has one.
    // A moment already past — zero. BCS does not document the header: an answer without it gives null.
    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        TimeSpan? wait = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - (response.Headers.Date ?? DateTimeOffset.UtcNow),
            _ => null,
        };

        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
    }

    private static string BuildMessage(HttpStatusCode statusCode, string? endpoint, BcsApiErrorResponse? apiError)
    {
        var message = "BCS API request failed.";

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            message += $" Endpoint='{endpoint}'.";
        }

        message += $" StatusCode={(int)statusCode} ({statusCode}).";

        if (!string.IsNullOrWhiteSpace(apiError?.Type))
        {
            message += $" Type='{apiError.Type}'.";
        }

        if (!string.IsNullOrWhiteSpace(apiError?.TraceId))
        {
            message += $" TraceId='{apiError.TraceId}'.";
        }

        return message;
    }
}
