namespace Bcs.InvestApi.DTO.Exceptions;

using System.Net;

public sealed class BcsApiException : Exception
{
    public BcsApiException(
        HttpStatusCode statusCode,
        string responseBody,
        string? endpoint = null)
        : base(BuildMessage(statusCode, endpoint))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
        Endpoint = endpoint;
    }

    public HttpStatusCode StatusCode { get; }

    public string ResponseBody { get; }

    public string? Endpoint { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? endpoint)
    {
        var message = "BCS API request failed.";

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            message += $" Endpoint='{endpoint}'.";
        }

        return $"{message} StatusCode={(int)statusCode} ({statusCode}).";
    }
}
