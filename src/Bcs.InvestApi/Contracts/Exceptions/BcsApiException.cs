namespace Bcs.InvestApi.Contracts.Exceptions;

using System.Net;
using Bcs.InvestApi.Contracts.Errors;

/// <summary>
/// Thrown when a BCS API endpoint returns a non-success HTTP status.
/// </summary>
/// <remarks>
/// The SDK does not retry. When <see cref="StatusCode"/> is
/// <see cref="HttpStatusCode.Unauthorized"/> with a <c>WWW-Authenticate: Bearer error="invalid_token"</c>
/// challenge, the cached access token is dropped before this exception is thrown, so the failing call
/// still surfaces the 401 but the next call transparently obtains a fresh token. Deciding whether to
/// retry that next call is the caller's responsibility.
/// </remarks>
public sealed class BcsApiException : Exception
{
    public BcsApiException(
        HttpStatusCode statusCode,
        string responseBody,
        string? endpoint = null,
        BcsApiErrorResponse? apiError = null)
        : base(BuildMessage(statusCode, endpoint, apiError))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
        Endpoint = endpoint;
        ApiError = apiError;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }

    public string? Endpoint { get; }

    public BcsApiErrorResponse? ApiError { get; }

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
