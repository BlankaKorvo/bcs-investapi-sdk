namespace Bcs.InvestApi.Infrastructure;

using System.Net;
using Bcs.InvestApi.Tokens;

internal static class BcsReadApiRequestExecutor
{
    public static async Task<string> SendAsync(
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender,
        Func<string, HttpRequestMessage> requestFactory,
        string endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var accessToken = await tokens
            .GetAccessTokenAsync(cancellationToken)
            .ConfigureAwait(false);
        using var exchange = await requestSender
            .SendAsync(httpClient, () => requestFactory(accessToken), cancellationToken)
            .ConfigureAwait(false);
        var response = exchange.Response;

        var responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        EnsureSuccess(response.StatusCode, responseBody, endpoint);
        return responseBody;
    }

    private static void EnsureSuccess(HttpStatusCode statusCode, string responseBody, string endpoint)
    {
        var statusCodeValue = (int)statusCode;
        if (statusCodeValue is >= 200 and <= 299)
        {
            return;
        }

        throw new BcsApiException(statusCode, responseBody, endpoint);
    }
}
