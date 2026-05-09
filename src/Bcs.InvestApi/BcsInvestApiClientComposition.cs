namespace Bcs.InvestApi;

using Bcs.InvestApi.Auth;
using Bcs.InvestApi.Infrastructure;
using Bcs.InvestApi.Instruments;
using Bcs.InvestApi.Limits;
using Bcs.InvestApi.MarketData;
using Bcs.InvestApi.Portfolio;
using Bcs.InvestApi.Time;
using Bcs.InvestApi.Tokens;
using Bcs.InvestApi.TradingSchedule;

internal static class BcsInvestApiClientComposition
{
    public const string AuthHttpClientName = "Bcs.InvestApi.Auth";

    public static void ConfigureAuthHttpClient(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        settings.ValidateTransportSettings();

        if (settings.Timeout is not null)
        {
            httpClient.Timeout = settings.Timeout.Value;
        }
    }

    public static BcsAuthService CreateAuthService(BcsInvestApiSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        return CreateAuthService(settings, httpClient, CreateHttpRequestSender());
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsAuthService(httpClient, settings, requestSender);
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        return CreateAuthService(settings, httpClientFactory, CreateHttpRequestSender());
    }

    public static BcsAuthService CreateAuthService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsAuthService(httpClientFactory, settings, requestSender);
    }

    public static IBcsHttpSender CreateHttpRequestSender() =>
        new BcsHttpRequestSender();

    public static BcsLimitsService CreateLimitsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsLimitsService(settings, httpClient, tokens, requestSender);
    }

    public static BcsLimitsService CreateLimitsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsLimitsService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsPortfolioService CreatePortfolioService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsPortfolioService(settings, httpClient, tokens, requestSender);
    }

    public static BcsPortfolioService CreatePortfolioService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsPortfolioService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsTradingScheduleService CreateTradingScheduleService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsTradingScheduleService(settings, httpClient, tokens, requestSender);
    }

    public static BcsTradingScheduleService CreateTradingScheduleService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsTradingScheduleService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsInstrumentsService CreateInstrumentsService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsInstrumentsService(settings, httpClient, tokens, requestSender);
    }

    public static BcsInstrumentsService CreateInstrumentsService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsInstrumentsService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsMarketDataService CreateMarketDataService(
        BcsInvestApiSettings settings,
        HttpClient httpClient,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsMarketDataService(settings, httpClient, tokens, requestSender);
    }

    public static BcsMarketDataService CreateMarketDataService(
        BcsInvestApiSettings settings,
        Func<HttpClient> httpClientFactory,
        IBcsAccessTokenProvider tokens,
        IBcsHttpSender requestSender)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requestSender);

        return new BcsMarketDataService(settings, httpClientFactory, tokens, requestSender);
    }

    public static BcsTokenManager CreateTokenManager(
        BcsAuthService authService,
        BcsInvestApiSettings settings,
        IBcsClock? clock)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(settings);

        return new BcsTokenManager(
            authService,
            settings,
            clock);
    }
}
