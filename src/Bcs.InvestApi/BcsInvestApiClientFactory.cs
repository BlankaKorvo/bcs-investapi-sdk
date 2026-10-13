namespace Bcs.InvestApi;

using Bcs.InvestApi.Contracts.Enums;
using Bcs.InvestApi.Services;
using Bcs.InvestApi.WebSockets;

public static class BcsInvestApiClientFactory
{
    /// <summary>
    /// Creates a client for <paramref name="refreshToken"/> with the explicit auth
    /// <paramref name="clientId"/> (there is no default: it decides whether the client may trade).
    /// </summary>
    public static BcsInvestApiClient Create(
        string refreshToken,
        BcsAuthClientIds clientId,
        HttpMessageHandler? httpMessageHandler = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("BCS refresh token is required.", nameof(refreshToken));
        }

        return Create(
            new BcsInvestApiSettings
            {
                RefreshToken = refreshToken,
                ClientId = clientId,
            },
            httpMessageHandler);
    }

    /// <summary>
    /// Creates a client from <paramref name="settings"/>. <paramref name="httpMessageHandler"/> and
    /// <paramref name="webSocketTransport"/> replace the HTTP and WebSocket transports (tests); by default
    /// the client uses its own <see cref="HttpClient"/> and <see cref="BcsClientWebSocketTransport"/>.
    /// </summary>
    public static BcsInvestApiClient Create(
        BcsInvestApiSettings settings,
        HttpMessageHandler? httpMessageHandler = null,
        TimeProvider? timeProvider = null,
        IBcsWebSocketTransport? webSocketTransport = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateRefreshToken();
        settings.ValidateAuthClientId();
        settings.ValidateTokenTimings();
        settings.ValidateWebSocketKeepAlive();

        var httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);

        BcsInvestApiHttpClientConfiguration.ConfigureHttpClient(settings, httpClient);

        var services = BcsInvestApiClientServices.Create(
            settings,
            httpClient,
            timeProvider,
            webSocketTransport: webSocketTransport);

        return services.CreateClient(
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }
}
