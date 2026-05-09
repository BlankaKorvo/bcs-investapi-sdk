namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Time;

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
        IBcsClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateTokenSettings();

        var httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);

        BcsInvestApiClientComposition.ConfigureAuthHttpClient(settings, httpClient);

        var requestSender = BcsInvestApiClientComposition.CreateAuthRequestSender(settings);
        var auth = BcsInvestApiClientComposition.CreateAuthService(settings, httpClient, requestSender);
        var tokens = BcsInvestApiClientComposition.CreateTokenManager(
            auth,
            settings,
            clock);
        var limits = BcsInvestApiClientComposition.CreateLimitsService(
            settings,
            httpClient,
            tokens,
            BcsInvestApiClientComposition.CreateReadHttpSender(settings));
        var portfolio = BcsInvestApiClientComposition.CreatePortfolioService(
            settings,
            httpClient,
            tokens,
            BcsInvestApiClientComposition.CreateReadHttpSender(settings));

        return new BcsInvestApiClient(
            auth,
            tokens,
            limits,
            portfolio,
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }
}
