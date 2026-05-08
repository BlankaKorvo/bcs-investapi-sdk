namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;

public static class BcsInvestApiClientFactory
{
    public static BcsInvestApiClient Create(
        string refreshToken,
        string clientId = BcsAuthClientIds.TradeApiRead,
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

    public static BcsInvestApiClient Create(
        BcsInvestApiSettings settings,
        HttpMessageHandler? httpMessageHandler = null,
        IBcsTokenStore? tokenStore = null,
        IBcsClock? clock = null,
        IBcsTokenRefreshCoordinator? tokenRefreshCoordinator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTokenSettings();

        var httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);

        if (settings.Timeout is not null)
        {
            httpClient.Timeout = settings.Timeout.Value;
        }

        tokenStore ??= CreateTokenStore(settings);

        var auth = new BcsAuthService(httpClient, settings);
        var tokens = new BcsTokenManager(auth, tokenStore, settings, clock, tokenRefreshCoordinator);

        return new BcsInvestApiClient(
            auth,
            tokens,
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }

    private static IBcsTokenStore CreateTokenStore(BcsInvestApiSettings settings) =>
        string.IsNullOrWhiteSpace(settings.TokenStoragePath)
            ? new BcsInMemoryTokenStore()
            : new BcsFileTokenStore(settings.TokenStoragePath);
}
