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

        BcsInvestApiClientComposition.ConfigureAuthHttpClient(settings, httpClient);

        tokenStore ??= BcsInvestApiClientComposition.CreateTokenStore(settings);

        var requestSender = BcsInvestApiClientComposition.CreateHttpRequestSender(settings);
        var auth = BcsInvestApiClientComposition.CreateAuthService(settings, httpClient, requestSender);
        var tokens = BcsInvestApiClientComposition.CreateTokenManager(
            auth,
            tokenStore,
            settings,
            clock,
            tokenRefreshCoordinator);

        return new BcsInvestApiClient(
            auth,
            tokens,
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }
}
