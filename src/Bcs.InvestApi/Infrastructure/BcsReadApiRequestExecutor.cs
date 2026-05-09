namespace Bcs.InvestApi.Infrastructure;

using System.Net;
using Bcs.InvestApi.Tokens;

internal static class BcsReadApiRequestExecutor
{
    public static async Task<string> SendAsync(
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsReadHttpSender requestSender,
        Func<string, HttpRequestMessage> requestFactory,
        string endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var accessToken = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var response = await SendOnceAsync(
            httpClient,
            requestSender,
            () => requestFactory(accessToken),
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            EnsureSuccess(response, endpoint);
            return response.Body;
        }

        var refreshedAccessToken = await TryRefreshAccessTokenAsync(tokens, cancellationToken).ConfigureAwait(false);
        if (refreshedAccessToken is null)
        {
            EnsureSuccess(response, endpoint);
            return response.Body;
        }

        var retryResponse = await SendOnceAsync(
            httpClient,
            requestSender,
            () => requestFactory(refreshedAccessToken),
            cancellationToken).ConfigureAwait(false);

        EnsureSuccess(retryResponse, endpoint);
        return retryResponse.Body;
    }

    private static async Task<BcsReadApiResponse> SendOnceAsync(
        HttpClient httpClient,
        IBcsReadHttpSender requestSender,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var exchange = await requestSender
            .SendAsync(httpClient, requestFactory, cancellationToken)
            .ConfigureAwait(false);
        var response = exchange.Response;

        var responseBody = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BcsReadApiResponse(response.StatusCode, responseBody);
    }

    private static async ValueTask<string?> TryRefreshAccessTokenAsync(
        IBcsAccessTokenProvider tokens,
        CancellationToken cancellationToken)
    {
        if (tokens is BcsTokenManager tokenManager)
        {
            var refreshedToken = await tokenManager.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return refreshedToken.AccessToken;
        }

        if (tokens is IBcsForcedAccessTokenRefreshProvider refreshProvider)
        {
            return await refreshProvider.RefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static void EnsureSuccess(BcsReadApiResponse response, string endpoint)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is >= 200 and <= 299)
        {
            return;
        }

        throw new BcsApiException(response.StatusCode, response.Body, endpoint);
    }

    private readonly record struct BcsReadApiResponse(
        HttpStatusCode StatusCode,
        string Body);
}

internal interface IBcsForcedAccessTokenRefreshProvider : IBcsAccessTokenProvider
{
    ValueTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);
}
