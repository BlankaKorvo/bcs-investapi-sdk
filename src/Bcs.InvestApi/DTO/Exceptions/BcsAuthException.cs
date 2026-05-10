namespace Bcs.InvestApi.DTO.Exceptions;

using System.Net;

public sealed class BcsAuthException : Exception
{
    public BcsAuthException(
        HttpStatusCode statusCode,
        string? error,
        string? errorDescription,
        string responseBody)
        : base(BuildMessage(statusCode, error))
    {
        StatusCode = statusCode;
        Error = error;
        ErrorDescription = errorDescription;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? Error { get; }

    public string? ErrorDescription { get; }

    public string ResponseBody { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? error)
    {
        var message = $"BCS auth request failed. StatusCode={(int)statusCode} ({statusCode}).";

        if (!string.IsNullOrWhiteSpace(error))
        {
            message += $" Error='{error}'.";
        }

        return message;
    }
}
