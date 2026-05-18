namespace Bcs.InvestApi.Contracts.Exceptions;

using System.Net;
using Bcs.InvestApi.Contracts.Errors;

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
