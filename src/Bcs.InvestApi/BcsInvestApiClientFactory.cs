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

        var requestSender = BcsInvestApiClientComposition.CreateHttpRequestSender();
        var auth = BcsInvestApiClientComposition.CreateAuthService(settings, httpClient, requestSender);
        var tokens = BcsInvestApiClientComposition.CreateTokenManager(
            auth,
            settings,
            clock);
        var limits = BcsInvestApiClientComposition.CreateLimitsService(
            settings,
            httpClient,
            tokens,
            requestSender);
        var portfolio = BcsInvestApiClientComposition.CreatePortfolioService(
            settings,
            httpClient,
            tokens,
            requestSender);
        var tradingSchedule = BcsInvestApiClientComposition.CreateTradingScheduleService(
            settings,
            httpClient,
            tokens,
            requestSender);
        var instruments = BcsInvestApiClientComposition.CreateInstrumentsService(
            settings,
            httpClient,
            tokens,
            requestSender);
        var marketData = BcsInvestApiClientComposition.CreateMarketDataService(
            settings,
            httpClient,
            tokens,
            requestSender);

        return new BcsInvestApiClient(
            auth,
            tokens,
            limits,
            portfolio,
            tradingSchedule,
            instruments,
            marketData,
            ownsTokenManager: true,
            ownedTransport: httpClient);
    }
}
